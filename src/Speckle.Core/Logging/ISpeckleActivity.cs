namespace Speckle.Core.Logging;

public interface ISpeckleActivity: IDisposable
{
  void SetTag(string key, object? value);
}
