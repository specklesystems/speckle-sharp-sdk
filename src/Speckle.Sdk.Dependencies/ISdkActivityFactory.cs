using System.Runtime.CompilerServices;
using Speckle.Connectors.Logging;

namespace Speckle.Sdk.Logging;

public interface ISdkActivityFactory : IDisposable
{
  ISdkActivity? Start(
    string? name = null,
    SdkActivityKind kind = SdkActivityKind.Internal,
    [CallerMemberName] string source = ""
  );

  ISdkActivity? StartRemote(
    string traceContext,
    SdkActivityKind kind,
    string? name = null,
    [CallerMemberName] string source = ""
  );
}
