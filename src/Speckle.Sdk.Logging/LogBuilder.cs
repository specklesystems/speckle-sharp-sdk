using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Exceptions;
using Serilog.Sinks.OpenTelemetry;

namespace Speckle.Sdk.Logging;

public static class LogBuilder
{
  public static IDisposable? Initialize(
    string userId,
    string applicationAndVersion,
    string slug,
    SpeckleLogging? speckleLogging,
    SpeckleTracing? speckleTracing
  )
  {
    var resourceBuilder = ResourceBuilder.CreateEmpty()
      .AddService(serviceName: Consts.Application, serviceVersion: Consts.Version)
      .AddAttributes(
        new List<KeyValuePair<string, object>>
        {
          new(Consts.SERVICE_NAME, applicationAndVersion), new(Consts.SERVICE_SLUG, slug), new(Consts.OS_NAME, Environment.OSVersion.ToString()), 
          new (Consts.OS_TYPE, RuntimeInformation.ProcessArchitecture.ToString()), new("runtime", RuntimeInformation.FrameworkDescription)
        }
      );
    var fileVersionInfo = GetFileVersionInfo();
    var serilogLogConfiguration = new LoggerConfiguration()
      .MinimumLevel.Is(SpeckleLogger.GetLevel(speckleLogging?.MinimumLevel ?? SpeckleLogLevel.Warning))
      .Enrich.FromLogContext()
      .Enrich.WithProperty("id", userId)
      .Enrich.WithProperty("version", fileVersionInfo.FileVersion)
      .Enrich.WithProperty("productVersion", fileVersionInfo.ProductVersion)
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
      serilogLogConfiguration = InitializeOtelLogging(serilogLogConfiguration, speckleLogging.Otel, resourceBuilder);
    }
    var logger = serilogLogConfiguration.CreateLogger();
    Log.Logger = logger;

    return InitializeOtelTracing(speckleTracing, resourceBuilder);
  }

  private static FileVersionInfo GetFileVersionInfo()
  {
    var assembly = Assembly.GetExecutingAssembly().Location;
    return FileVersionInfo.GetVersionInfo(assembly);
  }
  private static LoggerConfiguration InitializeOtelLogging(LoggerConfiguration serilogLogConfiguration, SpeckleOtelLogging speckleOtelLogging, ResourceBuilder resourceBuilder) =>
    serilogLogConfiguration.WriteTo.OpenTelemetry(o =>
    {
        
      o.Protocol = OtlpProtocol.HttpProtobuf;
      o.LogsEndpoint = speckleOtelLogging.Endpoint;
      o.Headers = speckleOtelLogging.Headers ?? o.Headers;
      o.ResourceAttributes = resourceBuilder.Build().Attributes.ToDictionary(x => x.Key, x => x.Value);
    });

  private static IDisposable? InitializeOtelTracing(SpeckleTracing? logConfiguration, ResourceBuilder resourceBuilder)
  {
    var consoleEnabled = logConfiguration?.Console ?? false;
    var otelEnabled = logConfiguration?.Otel?.Enabled ?? false;
    if (!consoleEnabled && !otelEnabled)
    {
      return null;
    }

    var tracerProviderBuilder = OpenTelemetry
      .Sdk.CreateTracerProviderBuilder()
      .AddSource(Consts.Application);
    tracerProviderBuilder = tracerProviderBuilder.AddHttpClientInstrumentation(
        (options) =>
        {
          options.FilterHttpWebRequest = (httpWebRequest) =>
          {
            // Example: Only collect telemetry about HTTP GET requests.
            return httpWebRequest.Method.Equals(HttpMethod.Get.Method);
          };
          options.EnrichWithHttpWebRequest = (activity, httpWebRequest) =>
          {
            activity.SetTag("requestVersion", httpWebRequest.ProtocolVersion);
          };
          // Note: Only called on .NET Framework.
          options.EnrichWithHttpWebResponse = (activity, httpWebResponse) =>
          {
            activity.SetTag("responseVersion", httpWebResponse.ProtocolVersion);
          };
          // Note: Only called on .NET & .NET Core runtimes.
          options.EnrichWithHttpRequestMessage = (activity, httpRequestMessage) =>
          {
            activity.SetTag("requestVersion", httpRequestMessage.Version);
          };
          // Note: Only called on .NET & .NET Core runtimes.
          options.EnrichWithHttpResponseMessage = (activity, httpResponseMessage) =>
          {
            activity.SetTag("responseVersion", httpResponseMessage.Version);
          };
          // Note: Called for all runtimes.
          options.EnrichWithException = (activity, exception) =>
          {
            activity.SetTag("stackTrace", exception.StackTrace);
          };
          options.RecordException = true;
        }
      );
    if (otelEnabled)
    {
      tracerProviderBuilder = tracerProviderBuilder.AddOtlpExporter(x => ProcessOptions(logConfiguration!, x));
    }

    if (consoleEnabled)
    {
      tracerProviderBuilder = tracerProviderBuilder.AddConsoleExporter();
    }
    tracerProviderBuilder =    tracerProviderBuilder
      .SetResourceBuilder(resourceBuilder);

    return tracerProviderBuilder.Build();
  }
     
     
  private static void ProcessOptions(SpeckleTracing logConfiguration, OtlpExporterOptions options)
  {
    options.Protocol = OtlpExportProtocol.HttpProtobuf;
    var headers = string.Join(",", logConfiguration.Otel?.Headers?.Select(x => x.Key + "=" + x.Value) ?? []);
    if (headers.Length != 0)
    {
      options.Headers = headers;
    }

    if (logConfiguration.Otel?.Endpoint is not null)
    {
      options.Endpoint = new Uri(logConfiguration.Otel.Endpoint);
    }
  }
}
