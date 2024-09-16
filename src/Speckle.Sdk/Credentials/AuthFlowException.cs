namespace Speckle.Sdk.Credentials;

public sealed class AuthFlowException : Exception
{
  public AuthFlowException(string? message, Exception? innerException)
    : base(message, innerException) { }

  public AuthFlowException(string? message)
    : base(message) { }

  public AuthFlowException() { }
}
