using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Speckle.Logging;

public class TraceBuilder(IDisposable? traceProvider) : IDisposable
{
  public static IDisposable Initialize(string application, SpeckleLogConfiguration logConfiguration)
  {
    var tracerProviderBuilder = Sdk.CreateTracerProviderBuilder().AddSource(application).AddZipkinExporter();

    if (logConfiguration.LogToConsole)
    {
      tracerProviderBuilder = tracerProviderBuilder.AddConsoleExporter();
    }

    return new TraceBuilder(tracerProviderBuilder.Build());
  }

  public void Dispose() => traceProvider?.Dispose();
}
