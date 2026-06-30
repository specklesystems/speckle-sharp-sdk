namespace Speckle.Sdk.Credentials;

public sealed class AuthFlowException : SpeckleException
{
  public AuthFlowException(string? message, Exception? innerException)
    : base(message, innerException) { }

  public AuthFlowException(string? message)
    : base(message) { }

  public AuthFlowException() { }
}

public class SpeckleAccountManagerException : SpeckleException
{
  public SpeckleAccountManagerException(string message)
    : base(message) { }

  public SpeckleAccountManagerException(string message, Exception? inner)
    : base(message, inner) { }

  public SpeckleAccountManagerException() { }
}
