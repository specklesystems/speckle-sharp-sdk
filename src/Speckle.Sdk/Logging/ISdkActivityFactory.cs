using System.Runtime.CompilerServices;

namespace Speckle.Sdk.Logging;

public interface ISdkActivityFactory : IDisposable
{
  ISdkActivity? Start(string? name = default, [CallerMemberName] string source = "");
}

public sealed class NullActivityFactory : ISdkActivityFactory
{
  public void Dispose() { }

  public ISdkActivity? Start(string? name = default, string source = "") => null;
}
