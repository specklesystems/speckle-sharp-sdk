namespace Speckle.Sdk.Logging;

public interface ISpeckleActivity : IDisposable
{
  void SetTag(string key, object? value);
}