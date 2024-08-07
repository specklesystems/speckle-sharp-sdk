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
  public static void Initialize(
    string userId,
    string applicationAndVersion,
    string? slug,
    SpeckleLogging? speckleLogging
  )
  {
    var fileVersionInfo = GetFileVersionInfo();
    var serilogLogConfiguration = new LoggerConfiguration()
      .MinimumLevel.Is(SpeckleLogger.GetLevel(speckleLogging?.MinimumLevel ?? SpeckleLogLevel.Warning))
      .Enrich.FromLogContext()
      .Enrich.WithProperty("id", userId)
      .Enrich.WithProperty("version", fileVersionInfo.FileVersion)
      .Enrich.WithProperty("productVersion", fileVersionInfo.ProductVersion)
      .Enrich.WithProperty("hostOs", DetermineHostOsSlug())
      .Enrich.WithProperty("hostOsVersion", Environment.OSVersion)
      .Enrich.WithProperty("hostOsArchitecture", RuntimeInformation.ProcessArchitecture.ToString())
      .Enrich.WithProperty("runtime", RuntimeInformation.FrameworkDescription)
      .Enrich.WithExceptionDetails();

    if (speckleLogging?.File is not null)
    {
      // TODO: check if we have write permissions to the file.
      var logFilePath = SpecklePathProvider.LogFolderPath(applicationAndVersion, slug);
      logFilePath = Path.Combine(logFilePath, speckleLogging.File.Path ?? "SpeckleCoreLog.txt");
      serilogLogConfiguration = serilogLogConfiguration.WriteTo.File(
        logFilePath,
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 10
      );
    }

    if (speckleLogging?.Console ?? false)
    {
      serilogLogConfiguration = serilogLogConfiguration.WriteTo.Console();
    }

    if (speckleLogging?.Otel is not null)
    {
      serilogLogConfiguration = serilogLogConfiguration.WriteTo.OpenTelemetry(o =>
      {
        o.Protocol = OtlpProtocol.HttpProtobuf;
        o.LogsEndpoint = speckleLogging.Otel.Endpoint;
        o.Headers = speckleLogging.Otel.Headers ?? o.Headers;
        o.ResourceAttributes = new Dictionary<string, object>
        {
          [Consts.SERVICE_NAME] = applicationAndVersion,
          [Consts.SERVICE_SLUG] = slug ?? string.Empty
        };
      });
    }
    var logger = serilogLogConfiguration.CreateLogger();
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
