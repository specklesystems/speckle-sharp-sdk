using Speckle.Sdk.Api.GraphQL.Resources;

namespace Speckle.Sdk.Common.Exceptions;

/// <summary>
/// Exception thrown when conversion cannot be performed "by design" based on input
/// </summary>
/// <remarks>Ideally, this exception should be thrown when we can pre-emptively check for invalid data that is known to cause an exception under normal circumstances</remarks>
/// <example>
/// It can be used for:
/// <ul>
///  <li> Invalid Speckle Objects (e.g. <c>"We don't support walls with null lines"</c>)</li>
///  <li> objects with a property whose value we don't support (e.g. <c>"Beams with shape type of Circular are not supported"</c>)</li>
///  <li> complex object requirements (e.g. <c>"We don't support walls with zero width and no displayValue"</c>)</li>
/// </ul>
/// It should <b>NOT</b> be used for:
/// <ul>
///  <li> Objects we have no top-level converter for</li>
///  <li> Objects that we have already converted, and therefore now skip (e.g. <c>"A Wall with the same name was already converted"</c>)</li>
///  <li> Reactive error handling (e.g. "Failed to convert wall, I guess it wasn't supported")</li>
///  <li> To wrap unexpected/general exceptions</li>
/// </ul>
/// </example>
public class ValidationException : SpeckleException
{
  public ValidationException() { }

  public ValidationException(string? message)
    : base(message) { }

  public ValidationException(string? message, Exception? innerException)
    : base(message, innerException) { }
}

/// <summary>
/// Thrown when an ingestion cannot be processed because of bad user input.
/// These are errors that are not indicative of a bug; we won't bother investigating them, they are not something that should be fixed.
/// </summary>
/// <remarks>
/// This should only be used for deterministic validation of designed support areas, such as:
/// <ul>
/// <li> - Users didn't select any supported geometry</li>
/// <li> - User selected a view that was not found</li>
/// <li> - User's uploaded a file that is an unsupported IFC schema/Revit version etc...</li>
/// </ul>
///
/// It should not be used to surface general/unexpected/unhandled errors (OOM, null reference, host app API exceptions),
/// or failure related to Speckle infrastructure (e.g. server's down, graphql requests failing)
/// </remarks>
/// <seealso cref="ModelIngestionResource.FailWithInvalid"/>
public class IngestionValidationException : SpeckleException
{
  public IngestionValidationException() { }

  public IngestionValidationException(string? message)
    : base(message) { }

  public IngestionValidationException(string? message, Exception? innerException)
    : base(message, innerException) { }
}
