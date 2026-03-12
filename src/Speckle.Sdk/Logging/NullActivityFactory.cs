namespace Speckle.Sdk.Logging;

public sealed class NullActivityFactory : ISdkActivityFactory
{
  public void Dispose() { }

  public ISdkActivity? Start(string? name = default, string source = "") => null;

  public ISdkActivity? StartRemote(string traceId, string parentSpanId, string? name = default, string source = "") =>
    null;
}
