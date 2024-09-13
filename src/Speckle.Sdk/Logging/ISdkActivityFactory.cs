namespace Speckle.Sdk.Logging;

public interface ISdkActivityFactory : IDisposable
{
  Speckle.Sdk.Logging.ISdkActivity? Start(string? name = default, string source = "");
}

public sealed class NullActivityFactory : ISdkActivityFactory
{
  public void Dispose() { }

  public ISdkActivity? Start(string? name = default, string source = "") => null;
}
