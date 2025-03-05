using Speckle.Objects.Other;
using Speckle.Sdk.Common;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Geometry;

/// <summary>
/// Flat polygon, defined by an outer boundary and inner loops.
/// </summary>
[SpeckleType("Objects.Geometry.Region")]
public class Region : Base, IHasArea, IHasBoundingBox, IDisplayValue<List<ICurve>>, ITransformable
{
  /// <summary>
  /// Boundary of a region.
  /// </summary>
  public required ICurve boundary { get; set; }

  /// <summary>
  /// Loops (voids) in the region.
  /// TODO: conditions for a valid object
  /// </summary>
  public required List<ICurve> innerLoops { get; set; } = new();

  /// <summary>
  /// The units this object's coordinates are in.
  /// This should be one of <see cref="Units"/>
  /// </summary>
  public required string units { get; set; }

  /// <summary>
  /// Indication whether the region is just a geometry (false) or has a hatch pattern (true).
  /// This is a useful distinction for receive in apps that support both Region and Hatch (aka region with hatch pattern)
  /// </summary>
  public required bool hasHatchPattern { get; set; }

  /// <inheritdoc/>
  public double area { get; set; }

  /// <inheritdoc/>
  public required Box? bbox { get; set; }

  /// <inheritdoc/>
  [DetachProperty]
  public required List<ICurve> displayValue { get; set; }

  /// <inheritdoc/>
  public bool TransformTo(Transform transform, out ITransformable transformed)
  {
    // assign self, in case transformation fails
    transformed = this;
    // transform boundary
    if (boundary is ITransformable boundaryTransformable)
    {
      boundaryTransformable.TransformTo(transform, out ITransformable transformedBoundary);

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

      // if boundary and loops transformations succeeded
      transformed = new Region
      {
        boundary = (ICurve)transformedBoundary,
        innerLoops = transformedLoops,
        hasHatchPattern = false,
        bbox = null, // TODO
        displayValue = new(), // TODO
        units = units,
      };
      return true;
    }

    return false;
  }
}
