using System.Runtime.CompilerServices;

namespace Speckle.Sdk.Logging;

public interface ISdkActivityFactory : IDisposable
{
  /// <param name="name"></param>
  /// <param name="source"></param>
  /// <param name="parentId">Only need to set if the parent is coming from an external source (e.g.to trace between client and server)</param>
  /// <returns></returns>
  ISdkActivity? Start(string? name = default, [CallerMemberName] string source = "", string? parentId = null);
}
