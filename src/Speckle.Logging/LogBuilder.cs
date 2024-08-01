using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Serilog;
using Serilog.Exceptions;
using Serilog.Sinks.OpenTelemetry;
using Speckle.Core.Helpers;

namespace Speckle.Logging;

public static class LogBuilder
{
  private static string s_logFolderPath;

  public static void Initialize(
    string userId,
    string hostApplication,
    string? slug,
    SpeckleLogging? logConfiguration
  )
  {
    logConfiguration ??= new();
    // TODO: check if we have write permissions to the file.
    // if not, disable file sink, even if its enabled in the config
    // show a warning about that...
    var canLogToFile = true;
    s_logFolderPath = SpecklePathProvider.LogFolderPath(hostApplication, slug);
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
      .Enrich.WithExceptionDetails();

    if (logConfiguration.File && canLogToFile)
    {
      serilogLogConfiguration = serilogLogConfiguration.WriteTo.File(
        logFilePath,
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 10
      );
    }

    if (logConfiguration.Console)
    {
      serilogLogConfiguration = serilogLogConfiguration.WriteTo.Console();
    }

    if (logConfiguration.Seq)
    {
      serilogLogConfiguration = serilogLogConfiguration.WriteTo.Seq(
        "https://seq.speckle.systems",
        apiKey: "agZqxG4jQELxQQXh0iZQ"
      );
    }
    if (logConfiguration.Otel)
    {
      serilogLogConfiguration = serilogLogConfiguration.WriteTo.OpenTelemetry(o =>
      {
        o.Protocol = OtlpProtocol.HttpProtobuf;
        o.LogsEndpoint = "https://seq.speckle.systems/ingest/otlp/v1/logs";
        o.Headers = new Dictionary<string, string> {
          { "X-Seq-ApiKey", "agZqxG4jQELxQQXh0iZQ"}};
        o.ResourceAttributes = new Dictionary<string, object>
        {
          [Consts.SERVICE_NAME] = hostApplication,
              [Consts.SERVICE_SLUG] = slug ?? string.Empty
        };
      });
    }
    var logger = serilogLogConfiguration.CreateLogger();

    if (logConfiguration.File && !canLogToFile)
    {
      logger.Warning("Log to file is enabled, but cannot write to {LogFilePath}", logFilePath);
    }
    Log.Logger = logger;
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
