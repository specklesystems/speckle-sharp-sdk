namespace Speckle.Core.Logging;

public class SpeckleException : Exception
{
  public SpeckleException() { }

  public SpeckleException(string? message)
    : base(message) { }

  public SpeckleException(string? message, Exception? inner = null)
    : base(message, inner) { }
}
