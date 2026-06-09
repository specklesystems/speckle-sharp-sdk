using Speckle.Connectors.Logging;

namespace Speckle.Sdk.Logging;

public sealed class NullActivityFactory : ISdkActivityFactory
{
  public void Dispose() { }

  public ISdkActivity? Start(
    string? name = null,
    SdkActivityKind kind = SdkActivityKind.Internal,
    IReadOnlyDictionary<string, object?>? tags = null,
    DateTimeOffset startTime = default,
    string source = ""
  ) => null;

  public ISdkActivity? StartRemote(
    string? traceParent,
    string? traceState,
    SdkActivityKind kind,
    string? name = null,
    IReadOnlyDictionary<string, object?>? tags = null,
    DateTimeOffset startTime = default,
    string source = ""
  ) => null;
}
