using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Serilog;
using Serilog.Exceptions;
using Speckle.Core.Common;
using Speckle.Core.Helpers;
using Microsoft.Extensions.Logging;
using Serilog.Extensions.Logging;

namespace Speckle.Core.Logging;

/// <summary>
/// Configuration object for the Speckle logging system.
/// </summary>
public sealed class SpeckleLogConfiguration
{
  /// <summary>
  /// Flag to enable enhanced log context. This adds the following enrich calls:
  /// - WithClientAgent
  /// - WithClientIp
  /// - WithExceptionDetails
  /// </summary>
  public bool EnhancedLogContext { get; }

  /// <summary>
  /// Flag to enable console sink
  /// </summary>
  public bool LogToConsole { get; }

  /// <summary>
  /// Flag to enable File sink
  /// </summary>
  public bool LogToFile { get; }

  /// <summary>
  /// Flag to enable Seq sink
  /// </summary>
  public bool LogToSeq { get; }

  /// <summary>
  /// Log events bellow this level are silently dropped
  /// </summary>
  public LogLevel MinimumLevel { get; }

  /// <summary>
  /// Flag to override the default Sentry DNS
  /// </summary>
  public string SentryDns { get; }

  private const string DEFAULT_SENTRY_DNS = "https://f29ec716d14d4121bb2a71c4f3ef7786@o436188.ingest.sentry.io/5396846";

  /// <summary>
  /// Default SpeckleLogConfiguration constructor.
  /// These are the sane defaults we should be using across connectors.
  /// </summary>
  /// <param name="logToConsole">Flag to enable console log sink</param>
  /// <param name="logToSeq">Flag to enable Seq log sink</param>
  /// <param name="logToFile">Flag to enable File log sink</param>
  /// <param name="enhancedLogContext">Flag to enable enhanced context on every log event</param>
  public SpeckleLogConfiguration(
    bool logToConsole = true,
    bool logToSeq = true,
    bool logToFile = true,
    bool enhancedLogContext = true,
    string sentryDns = DEFAULT_SENTRY_DNS
  )
  {
    MinimumLevel = LogLevel.Debug;
    LogToConsole = logToConsole;
    LogToSeq = logToSeq;
    LogToFile = logToFile;
    EnhancedLogContext = enhancedLogContext;
    SentryDns = sentryDns;
  }
}

/// <summary>
/// Configurator class for a standardized logging system across Speckle (sharp).
/// </summary>
public sealed class SpeckleLog
{
  public static ILoggerProvider GetProvider() => new SerilogLoggerProvider(Serilog.Log.Logger);
  private static Microsoft.Extensions.Logging.ILogger? s_logger;

  public static  Microsoft.Extensions.Logging.ILogger Logger
  {
    get
    {
      if (s_logger == null)
      {
        Initialize("Speckle.Core", "unknown");
      }

      return s_logger.NotNull();
    }
  }

  private static bool s_initialized;

  private static bool s_isMachineIdUsed;

  private static string s_logFolderPath;

  /// <summary>
  /// Initialize logger configuration for a global Serilog.Log logger.
  /// </summary>
  public static void Initialize(
    string hostApplicationName,
    string? hostApplicationVersion,
    SpeckleLogConfiguration? logConfiguration = null
  )
  {
    if (s_initialized)
    {
      Log.Logger
        .ForContext("hostApplicationVersion", hostApplicationVersion)
        .ForContext("hostApplicationName", hostApplicationName)
        .Information("Setup was already initialized");
      return;
    }

    logConfiguration ??= new SpeckleLogConfiguration();

    s_logger = CreateConfiguredLogger(hostApplicationName, hostApplicationVersion, logConfiguration);

    Log.Logger
      .ForContext("userApplicationDataPath", SpecklePathProvider.UserApplicationDataPath())
      .ForContext("installApplicationDataPath", SpecklePathProvider.InstallApplicationDataPath)
      .ForContext("speckleLogConfiguration", logConfiguration)
      .Information(
        "Initialized logger inside {hostApplication}/{productVersion}/{version} for user {id}. Path info {userApplicationDataPath} {installApplicationDataPath}."
      );

    s_initialized = true;
  }

  /// <summary>
  /// Create a new fully configured Logger instance.
  /// </summary>
  /// <param name="hostApplicationName">Name of the application using this SDK ie.: "Rhino"</param>
  /// <param name="hostApplicationVersion">Public version slug of the application using this SDK ie.: "2023"</param>
  /// <param name="logConfiguration">Input configuration object.</param>
  /// <returns>Logger instance</returns>
  private static Microsoft.Extensions.Logging.ILogger CreateConfiguredLogger(
    string hostApplicationName,
    string? hostApplicationVersion,
    SpeckleLogConfiguration logConfiguration
  )
  {
    // TODO: check if we have write permissions to the file.
    // if not, disable file sink, even if its enabled in the config
    // show a warning about that...
    var canLogToFile = true;
    s_logFolderPath = SpecklePathProvider.LogFolderPath(hostApplicationName, hostApplicationVersion);
    var logFilePath = Path.Combine(s_logFolderPath, "SpeckleCoreLog.txt");

    var fileVersionInfo = GetFileVersionInfo();
    var serilogLogConfiguration = new LoggerConfiguration()
      .Enrich.FromLogContext()
      .Enrich.WithProperty("version", fileVersionInfo.FileVersion)
      .Enrich.WithProperty("productVersion", fileVersionInfo.ProductVersion)
      .Enrich.WithProperty("hostOs", DetermineHostOsSlug())
      .Enrich.WithProperty("hostOsVersion", Environment.OSVersion)
      .Enrich.WithProperty("hostOsArchitecture", RuntimeInformation.ProcessArchitecture.ToString())
      .Enrich.WithProperty("runtime", RuntimeInformation.FrameworkDescription)
      .Enrich.WithProperty("hostApplication", $"{hostApplicationName}{hostApplicationVersion ?? ""}");

    if (logConfiguration.EnhancedLogContext)
    {
      serilogLogConfiguration = serilogLogConfiguration
        .Enrich.WithRequestHeader("User-Agent")
        .Enrich.WithClientIp()
        .Enrich.WithExceptionDetails();
    }

    if (logConfiguration.LogToFile && canLogToFile)
    {
      serilogLogConfiguration = serilogLogConfiguration.WriteTo.File(
        logFilePath,
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 10
      );
    }

    if (logConfiguration.LogToConsole)
    {
      serilogLogConfiguration = serilogLogConfiguration.WriteTo.Console();
    }

    if (logConfiguration.LogToSeq)
    {
      serilogLogConfiguration = serilogLogConfiguration.WriteTo.Seq(
        "https://seq.speckle.systems",
        apiKey: "agZqxG4jQELxQQXh0iZQ"
      );
    }

    var logger = serilogLogConfiguration.CreateLogger();

    if (logConfiguration.LogToFile && !canLogToFile)
    {
      logger.Warning("Log to file is enabled, but cannot write to {LogFilePath}", logFilePath);
    }

    if (s_isMachineIdUsed)
    {
      logger.Warning("Cannot set user id for the global log context.");
    }
    var id = GetUserIdFromDefaultAccount();
    var l = logger.ForContext("id", id).ForContext("isMachineId", s_isMachineIdUsed);

    Serilog.Log.Logger = l;
    var factory = new LoggerFactory(Array.Empty<ILoggerProvider>(), new LoggerFilterOptions() { MinLevel = logConfiguration.MinimumLevel }).AddSerilog(Log.Logger);
    return factory.CreateLogger<SpeckleLog>();
  }

  private static string GetUserIdFromDefaultAccount()
  {
    var machineName = Environment.MachineName;
    var userName = Environment.UserName;
    var id = Crypt.Md5($"{machineName}:{userName}", "X2");
    try
    {
      /*
      var defaultAccount = AccountManager.GetDefaultAccount();
      if (defaultAccount != null)
      {
        id = defaultAccount.GetHashedEmail();
      }
      else
      {*/
        s_isMachineIdUsed = true;
      //}
    }
    catch (Exception ex) when (!ex.IsFatal())
    {
      // To log it after Logger initialized as deferred action.
      s_isMachineIdUsed = true;
    }
    return id;
  }

  private static FileVersionInfo GetFileVersionInfo()
  {
    var assembly = Assembly.GetExecutingAssembly().Location;
    return FileVersionInfo.GetVersionInfo(assembly);
  }

  private static string DetermineHostOsSlug()
  {
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
      return "Windows";
    }

    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
    {
      return "MacOS";
    }

    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
    {
      return "Linux";
    }

    return RuntimeInformation.OSDescription;
  }
}
