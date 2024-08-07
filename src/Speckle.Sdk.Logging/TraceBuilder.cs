using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Speckle.Sdk.Logging;

public class TraceBuilder(IDisposable? traceProvider) : IDisposable
{
  public static IDisposable? Initialize(string applicationAndVersion, string slug, SpeckleTracing? logConfiguration)
  {
    var consoleEnabled = logConfiguration?.Console ?? false;
    var otelEnabled = logConfiguration?.Otel?.Enabled ?? false;
    if (!consoleEnabled && !otelEnabled)
    {
      return null;
    }

    var tracerProviderBuilder = OpenTelemetry
      .Sdk.CreateTracerProviderBuilder()
      .AddSource(slug)
      .ConfigureResource(r =>
      {
        r.AddAttributes(
          new List<KeyValuePair<string, object>>
          {
            new(Consts.SERVICE_NAME, applicationAndVersion),
            new(Consts.SERVICE_SLUG, slug)
          }
        );
      })
      .AddHttpClientInstrumentation();
    if (otelEnabled)
    {
      var headers = string.Join(",", logConfiguration?.Otel?.Headers?.Select(x => x.Key + "=" + x.Value) ?? []);
      tracerProviderBuilder = tracerProviderBuilder.AddOtlpExporter(x =>
      {
        x.Protocol = OtlpExportProtocol.HttpProtobuf;
        x.Endpoint = new Uri(logConfiguration!.Otel!.Endpoint);
        x.Headers = headers;
      });
    }

    if (consoleEnabled)
    {
      tracerProviderBuilder = tracerProviderBuilder.AddConsoleExporter();
    }

    return new TraceBuilder(tracerProviderBuilder.Build());
  }

  public void Dispose() => traceProvider?.Dispose();
}
