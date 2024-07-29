using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Speckle.Core.Common;

namespace Speckle.Core.Logging;

public class SpeckleActivity(Activity activity) : ISpeckleActivity
{
  public void Dispose() => activity.Dispose();

  public void SetTag(string key, object? value) =>activity.SetTag(key, value);
}

public class SpeckleActivityFactory(string application) : ISpeckleActivityFactory
{
  private readonly ActivitySource _activitySource = new(application);

  public ISpeckleActivity StartActivity(string name) => new SpeckleActivity(_activitySource.StartActivity(name).NotNull());
}
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
