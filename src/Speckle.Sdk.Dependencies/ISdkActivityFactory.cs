using System.Runtime.CompilerServices;
using Speckle.Connectors.Logging;

namespace Speckle.Sdk.Logging;

public interface ISdkActivityFactory : IDisposable
{
  ISdkActivity? Start(
    string? name = null,
    SdkActivityKind kind = SdkActivityKind.Internal,
    IReadOnlyDictionary<string, object?>? tags = null,
    DateTimeOffset startTime = default,
    [CallerMemberName] string source = ""
  );

  ISdkActivity? StartRemote(
    string? traceParent,
    string? traceState,
    SdkActivityKind kind,
    string? name = null,
    IReadOnlyDictionary<string, object?>? tags = null,
    DateTimeOffset startTime = default,
    [CallerMemberName] string source = ""
  );
}
