namespace Speckle.Logging;

public interface ISpeckleActivity: IDisposable
{
  void SetTag(string key, object? value);
}
