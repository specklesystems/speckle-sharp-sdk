namespace Speckle.Sdk.Common.Exceptions;

/// <summary>
/// Exception thrown when conversion of an object was not successful
/// </summary>
/// <remarks>
/// Ideally this exception contains a meaningful message.
/// This exception can be used for both ToSpeckle and ToNative conversion
/// </remarks>
public class ConversionException : SpeckleException
{
  public ConversionException(string? message, Exception? innerException)
    : base(message, innerException) { }

  public ConversionException(string? message)
    : base(message) { }

  public ConversionException() { }
}
