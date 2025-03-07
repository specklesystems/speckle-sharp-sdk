namespace Speckle.Sdk.Credentials;

public class SpeckleAccountManagerException : SpeckleException
{
  public SpeckleAccountManagerException(string message)
    : base(message) { }

  public SpeckleAccountManagerException(string message, Exception? inner)
    : base(message, inner) { }

  public SpeckleAccountManagerException() { }
}

public class SpeckleAccountFlowLockedException : SpeckleAccountManagerException
{
  public SpeckleAccountFlowLockedException(string message)
    : base(message) { }

  public SpeckleAccountFlowLockedException() { }

  public SpeckleAccountFlowLockedException(string message, Exception? innerException)
    : base(message, innerException) { }
}
