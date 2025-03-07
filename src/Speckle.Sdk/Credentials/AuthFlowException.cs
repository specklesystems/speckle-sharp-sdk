namespace Speckle.Sdk.Credentials;

#pragma warning disable CA2237
public sealed class AuthFlowException : Exception
#pragma warning restore CA2237
{
  public AuthFlowException(string? message, Exception? innerException)
    : base(message, innerException) { }

  public AuthFlowException(string? message)
    : base(message) { }

  public AuthFlowException() { }
}
