using System.Runtime.InteropServices;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Speckle.Sdk.Logging;

public static class TracingBuilder
{
  public static IDisposable? Initialize(string applicationAndVersion, string slug, SpeckleTracing? speckleTracing)
  {
    var resourceBuilder = ResourceBuilder
      .CreateEmpty()
      .AddService(serviceName: Consts.Application, serviceVersion: Consts.Version)
      .AddAttributes(
        new List<KeyValuePair<string, object>>
        {
          new(Consts.SERVICE_NAME, applicationAndVersion),
          new(Consts.SERVICE_SLUG, slug),
          new(Consts.OS_NAME, Environment.OSVersion.ToString()),
          new(Consts.OS_TYPE, RuntimeInformation.ProcessArchitecture.ToString()),
          new(Consts.OS_SLUG, DetermineHostOsSlug()),
          new(Consts.RUNTIME_NAME, RuntimeInformation.FrameworkDescription)
        }
      );

    return InitializeOtelTracing(speckleTracing, resourceBuilder);
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

  private static IDisposable? InitializeOtelTracing(SpeckleTracing? logConfiguration, ResourceBuilder resourceBuilder)
  {
    var consoleEnabled = logConfiguration?.Console ?? false;
    var otelEnabled = logConfiguration?.Otel?.Enabled ?? false;
    if (!consoleEnabled && !otelEnabled)
    {
      return null;
    }

    var tracerProviderBuilder = OpenTelemetry.Sdk.CreateTracerProviderBuilder().AddSource(Consts.Application);
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

    tracerProviderBuilder = tracerProviderBuilder.SetResourceBuilder(resourceBuilder).SetSampler<AlwaysOnSampler>();

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
