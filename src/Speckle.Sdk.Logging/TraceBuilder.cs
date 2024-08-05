using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Speckle.Sdk.Logging;

public class TraceBuilder(IDisposable? traceProvider) : IDisposable
{
  public static IDisposable Initialize(string application, SpeckleLogConfiguration logConfiguration)
  {
    var tracerProviderBuilder = OpenTelemetry
      .Sdk.CreateTracerProviderBuilder()
      .AddSource(application)
      .ConfigureResource(r =>
      {
        r.AddAttributes(new List<KeyValuePair<string, object>> { new("service.name", application) });
      })
      .AddHttpClientInstrumentation();
    if (logConfiguration.LogToOtel)
    {
      tracerProviderBuilder = tracerProviderBuilder.AddOtlpExporter(x =>
      {
        x.Protocol = OtlpExportProtocol.HttpProtobuf;
      });
    }
    if (logConfiguration.LogToConsole)
    {
      tracerProviderBuilder = tracerProviderBuilder.AddConsoleExporter();
    }
    return new TraceBuilder(tracerProviderBuilder.Build());
  }

  public void Dispose() => traceProvider?.Dispose();
}
