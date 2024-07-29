namespace Speckle.Core.Logging;

public interface ISpeckleActivityFactory
{
  ISpeckleActivity StartActivity(string name);
}
