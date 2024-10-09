namespace Speckle.Sdk.Common.Exceptions;

/// <summary>
/// Exception used when an object could not be converted, because we don't support a specific conversion.
/// </summary>
/// <remarks>
/// This Exception should be thrown only when a top-level converter does not exist.</remarks>
/// <example>
/// It should <b>NOT</b> be used for:
/// <ul>
///  <li> objects who's <see cref="Type"/> we don't support (e.g. <c>"Walls are not supported"</c>)</li>
///  <li> objects with a property whose value we don't support (e.g. <c>"Beams with shape type of Circular are not supported"</c>)</li>
///  <li> complex object requirements (e.g. <c>"We don't support walls with zero width and no displayValue"</c>)</li>
///  <li> Invalid Speckle Objects (e.g. <c>"We don't support walls with null lines"</c>)</li>
///  <li> Objects that we have already converted, and therefore now skip (e.g. <c>"A Wall with the same name was already converted"</c>)</li>
///  <li> Reactive error handling (e.g. "Failed to convert wall, I guess it wasn't supported")</li>
/// </ul>
/// </example>
public class ConversionNotSupportedException : ConversionException
{
  public ConversionNotSupportedException(string message, Exception innerException)
    : base(message, innerException) { }

  public ConversionNotSupportedException(string message)
    : base(message) { }

  public ConversionNotSupportedException() { }
}
