namespace Speckle.Sdk.Serialisation;

public class SpeckleDeserializeException : SpeckleException
{
  public SpeckleDeserializeException(string message, Exception? inner = null)
    : base(message, inner) { }

  public SpeckleDeserializeException(string message)
    : base(message) { }
}
