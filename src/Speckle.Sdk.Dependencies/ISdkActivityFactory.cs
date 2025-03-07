using System.Runtime.CompilerServices;

namespace Speckle.Sdk.Logging;

public interface ISdkActivityFactory : IDisposable
{
  ISdkActivity? Start(string? name = default, [CallerMemberName] string source = "");
}
