using Speckle.Connectors.Logging;

namespace Speckle.Sdk.Logging;

public sealed class NullActivityFactory : ISdkActivityFactory
{
  public void Dispose() { }

  public ISdkActivity? Start(string? name, SdkActivityKind kind, string source) => null;

  public ISdkActivity? StartRemote(string traceContext, SdkActivityKind kind, string? name, string source) => null;
}
