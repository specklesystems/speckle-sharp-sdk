using Speckle.Objects.Other;
using Speckle.Sdk.Common;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Geometry;

/// <summary>
/// Flat polygon, defined by an outer boundary and inner loops.
/// </summary>
[SpeckleType("Objects.Geometry.Region")]
public class Region : Base, IHasArea, IHasBoundingBox, ITransformable
{
  /// <summary>
  /// Boundary of a region.
  /// Should be a planar, closed, non-self-intersecting ICurve.
  /// </summary>
  public required ICurve boundary { get; set; }

  /// <summary>
  /// Loops (voids) in the region.
  /// Each loop should be planar, closed, non-self-intersecting ICurve, located inside the boundary.
  /// The loops should not intersect or touch each other.
  /// </summary>
  public required List<ICurve> innerLoops { get; set; } = new();

  /// <summary>
  /// The units of object's coordinates.
  /// This should be one of <see cref="Units"/>
  /// </summary>
  public required string units { get; set; }

  /// <summary>
  /// Indication whether the region is just a geometry (false) or has a hatch pattern (true).
  /// It's a distinction for receiving in apps that support both Region and Hatch (aka region with hatch pattern)
  /// </summary>
  public required bool hasHatchPattern { get; set; }

  /// <inheritdoc/>
  public double area { get; set; }

  /// <inheritdoc/>
  public required Box? bbox { get; set; }

  /// <inheritdoc/>
  [DetachProperty]
  public List<Mesh> displayValue { get; set; } = new();

  /// <inheritdoc/>
  public bool TransformTo(Transform transform, out ITransformable transformed)
  {
    // assign self to the returned object, in case transformation fails
    transformed = this;

    // transform boundary
    if (boundary is ITransformable boundaryTransformable)
    {
      boundaryTransformable.TransformTo(transform, out ITransformable transformedBoundaryResult);
      var transformedBoundary = (ICurve)transformedBoundaryResult;

      // transform inner loops
      var transformedLoops = new List<ICurve>();
      foreach (var loop in innerLoops)
      {
        if (loop is ITransformable loopTransformable)
        {
          loopTransformable.TransformTo(transform, out ITransformable transformedLoop);
          transformedLoops.Add((ICurve)transformedLoop);
        }
        else
        {
          return false;
        }
      }

      // TODO: transform mesh of display value
      transformed = new Region
      {
        boundary = transformedBoundary,
        innerLoops = transformedLoops,
        hasHatchPattern = hasHatchPattern,
        bbox = null, // maybe calculate in the future if we make Box ITransformable
        units = units,
      };
      return true;
    }

    return false;
  }
}
