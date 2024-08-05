using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Serilog;
using Serilog.Exceptions;
using Serilog.Sinks.OpenTelemetry;

namespace Speckle.Logging;

public static class LogBuilder
{
  private static string s_logFolderPath;

  private static readonly object s_lock = new();
  private static bool s_initialized;

  public static void Initialize(
    string userId,
    string hostApplicationName,
    string? hostApplicationVersion,
    SpeckleLogConfiguration logConfiguration
  )
  {
    if (!s_initialized)
    {
      lock (s_lock)
      {
        if (!s_initialized)
        {
          var logger = CreateConfiguredLogger(userId, hostApplicationName, hostApplicationVersion, logConfiguration);
          Log.Logger = logger;
          s_initialized = true;
        }
      }
    }
  }

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
      .Enrich.WithProperty("runtime", RuntimeInformation.FrameworkDescription);

    if (logConfiguration.EnhancedLogContext)
    {
      serilogLogConfiguration = serilogLogConfiguration.Enrich.WithExceptionDetails();
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
    if (logConfiguration.LogToOtel)
    {
      serilogLogConfiguration = serilogLogConfiguration.WriteTo.OpenTelemetry(o =>
      {
        o.Protocol = OtlpProtocol.HttpProtobuf;
        o.ResourceAttributes = new Dictionary<string, object>
        {
          ["service.name"] = $"{hostApplicationName}{hostApplicationVersion ?? ""}"
        };
      });
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
