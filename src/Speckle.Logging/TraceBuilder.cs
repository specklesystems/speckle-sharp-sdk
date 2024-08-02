using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Speckle.Logging;

public class TraceBuilder(IDisposable? traceProvider) : IDisposable
{
  public static IDisposable? Initialize(string application, string? slug, SpeckleTracing? logConfiguration)
  {
    logConfiguration ??= new();
    if (logConfiguration is { Otel: false, Console: false, Seq: false })
    {
      return null;
    }

    {
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
      if (logConfiguration.Otel || logConfiguration.Seq)
      {
        tracerProviderBuilder = tracerProviderBuilder.AddOtlpExporter(x =>
        {
          x.Protocol = OtlpExportProtocol.HttpProtobuf;
          x.Endpoint = new Uri("https://seq.speckle.systems/ingest/otlp/v1/traces");
          x.Headers = "X-Seq-ApiKey=agZqxG4jQELxQQXh0iZQ";
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
