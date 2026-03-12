using System.Runtime.CompilerServices;

namespace Speckle.Sdk.Logging;

public interface ISdkActivityFactory : IDisposable
{
  /// <param name="name"></param>
  /// <param name="source"></param>
  /// <returns></returns>
  ISdkActivity? Start(string? name = default, [CallerMemberName] string source = "");
  ISdkActivity? StartRemote(
    string traceId,
    string parentSpanId,
    string? name = default,
    [CallerMemberName] string source = ""
  );
}
