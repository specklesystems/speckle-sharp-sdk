namespace Speckle.Sdk.Logging;

public sealed class NullActivityFactory : ISdkActivityFactory
{
  public void Dispose() { }

  public ISdkActivity? Start(string? name = default, string source = "") => null;
}
