using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Speckle.Core.Logging;

public class OpenTelemetryBuilder(IDisposable? traceProvider) : IDisposable
{
  public static IDisposable Initialize(string application)
  {
    var tracerProvider = Sdk.CreateTracerProviderBuilder()
      .AddSource(application)
      .AddZipkinExporter()
      .Build();

    return new OpenTelemetryBuilder(tracerProvider);
  }

  public void Dispose()
  {
    traceProvider?.Dispose();
  }
}
