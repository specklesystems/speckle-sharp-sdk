namespace Speckle.Sdk.Common.Exceptions;

/// <summary>
/// Exception thrown when a unit is encountered that is not supported, either by Speckle or the host app.
/// </summary>
public class UnitNotSupportedException : SpeckleException
{
  public UnitNotSupportedException() { }

  public UnitNotSupportedException(string message)
    : base(message) { }

  public UnitNotSupportedException(string message, Exception innerException)
    : base(message, innerException) { }
}
