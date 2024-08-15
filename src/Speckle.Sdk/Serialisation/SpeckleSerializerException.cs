namespace Speckle.Sdk.Serialisation;

public class SpeckleSerializeException : SpeckleException
{

  public SpeckleSerializeException(string message, Exception? inner = null)
    : base(message, inner) { }

  public SpeckleSerializeException(string message)
    : base(message) { }
}
