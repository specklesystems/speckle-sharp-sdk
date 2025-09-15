namespace Speckle.Sdk.Caching;

public class ModelManagerException : SpeckleException
{
  public ModelManagerException() { }

  public ModelManagerException(string message)
    : base(message) { }

  public ModelManagerException(string message, Exception inner)
    : base(message, inner) { }
}
