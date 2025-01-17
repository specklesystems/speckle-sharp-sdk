using System.Diagnostics.CodeAnalysis;
using Speckle.Objects.Other;

namespace Speckle.Objects;

public static class TransformableExtensions
{
  /// <inheritdoc cref="ITransformable{T}.TransformTo"/>
  public static ITransformable TransformTo(this ITransformable transformable, Transform transform)
  {
    return ((ITransformable<ITransformable>)transformable).TransformTo(transform);
  }

  public static bool TransformTo<T>(
    this ITransformable transformable,
    Transform transform,
    [NotNull] out T? transformed
  )
    where T : class, ITransformable
  {
    // try
    // {
    transformed = (T)transformable.TransformTo(transform);
    return true;
    // }
    // catch (TransformationException)
    // {
    //   transformed = null;
    //   return false;
    // }
  }
}
