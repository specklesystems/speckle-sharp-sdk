using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Serilog;
using Serilog.Core;
using Serilog.Exceptions;
using Speckle.Core.Helpers;

namespace Speckle.Logging;

public static class SpeckleLog
{
  private static string s_logFolderPath;

  private static readonly object _lock = new object();
  private static bool initialized = false;

  public static void Initialize(
    string userId,
    string hostApplicationName,
    string? hostApplicationVersion,
    SpeckleLogConfiguration logConfiguration
  )
  {
    if (!initialized)
    {
      lock (_lock)
      {
        if (!initialized)
        {
          var logger = CreateConfiguredLogger(userId, hostApplicationName, hostApplicationVersion, logConfiguration);
          Log.Logger = logger;
        }
      }
    }
  }

  public static ISpeckleLogger Logger => new SpeckleLogger(Serilog.Log.Logger);
  public static ISpeckleLogger Create(string name) => new SpeckleLogger(Log.Logger.ForContext(Constants.SourceContextPropertyName, name));

  private static Serilog.ILogger CreateConfiguredLogger(
    string userId,
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
      .Enrich.WithProperty("id", userId)
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
    return logger;
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
