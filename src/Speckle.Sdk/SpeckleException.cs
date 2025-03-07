namespace Speckle.Sdk;

#pragma warning disable CA2237
public class SpeckleException : Exception
#pragma warning restore CA2237
{
  public SpeckleException() { }

  public SpeckleException(string? message)
    : base(message) { }

  public SpeckleException(string? message, Exception? inner = null)
    : base(message, inner) { }
}
