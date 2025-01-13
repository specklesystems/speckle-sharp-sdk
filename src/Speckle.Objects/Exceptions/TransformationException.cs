using Speckle.Sdk;

namespace Speckle.Objects.Exceptions;

/// <summary>
/// <see cref="ITransformable{T}"/> object failed to transform
/// </summary>
public sealed class TransformationException : SpeckleException
{
  public TransformationException() { }

  public TransformationException(string? message)
    : base(message) { }

  public TransformationException(string? message, Exception? innerException)
    : base(message, innerException) { }
}
