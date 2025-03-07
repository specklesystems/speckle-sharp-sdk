namespace Speckle.Sdk.Logging;

public sealed class NullSdkMetricsFactory : ISdkMetricsFactory
{
  public ISdkCounter<T> CreateCounter<T>(string name, string? unit = default, string? description = default)
    where T : struct => new NullSdkCounter<T>();
}
