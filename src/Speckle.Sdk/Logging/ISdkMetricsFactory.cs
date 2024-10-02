namespace Speckle.Sdk.Logging;

public interface ISdkMetricsFactory
{
  ISdkCounter<T> CreateCounter<T>(string name, string? unit = default, string? description = default) 
    where T : struct;
}
