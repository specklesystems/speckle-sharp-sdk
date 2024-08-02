using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Speckle.Logging;

public class TraceBuilder(IDisposable? traceProvider) : IDisposable
{
  public static IDisposable? Initialize(string application, string? slug, SpeckleTracing? logConfiguration)
  {
    if (!(logConfiguration?.Console ?? false) 
        || (logConfiguration.Otel?.Enabled ?? false))
    {
      return null;
    }

    {
      Uri GetUri()
      {
        if (logConfiguration.Otel?.Endpoint is null)
        {
          return new Uri("https://seq.speckle.systems/ingest/otlp/v1/traces");
        }

        return new Uri(logConfiguration.Otel.Endpoint);
      }
      var tracerProviderBuilder = Sdk.CreateTracerProviderBuilder()
        .AddSource(application)
        .ConfigureResource(r =>
        {
          r.AddAttributes(
            new List<KeyValuePair<string, object>>
            {
              new(Consts.SERVICE_NAME, application),
              new(Consts.SERVICE_SLUG, slug ?? string.Empty)
            }
          );
        })
        .AddHttpClientInstrumentation();
      if (logConfiguration.Otel is not null)
      {
        tracerProviderBuilder = tracerProviderBuilder.AddOtlpExporter(x =>
        {
          x.Protocol = OtlpExportProtocol.HttpProtobuf;
          x.Endpoint = GetUri();
          x.Headers = string.Join(";", logConfiguration.Otel?.Headers?.Select((k,v) => k + " " + v) ?? []);
        });
      }

      if (logConfiguration.Console)
      {
        tracerProviderBuilder = tracerProviderBuilder.AddConsoleExporter();
      }

      return new TraceBuilder(tracerProviderBuilder.Build());
    }
  }

  public void Dispose() => traceProvider?.Dispose();
}
