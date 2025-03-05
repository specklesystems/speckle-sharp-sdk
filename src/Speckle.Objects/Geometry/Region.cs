using Speckle.Objects.Other;
using Speckle.Sdk.Common;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Geometry;

/// <summary>
/// Flat polygon, defined by an outer boundary and inner loops.
/// </summary>
[SpeckleType("Objects.Geometry.Region")]
public class Region : Base, IHasArea, IHasBoundingBox, IDisplayValue<List<Mesh>>, ITransformable
{
  /// <summary>
  /// Boundary of a region.
  /// </summary>
  public required ICurve boundary { get; set; }

  /// <summary>
  /// Loops (voids) in the region.
  /// </summary>
  public List<ICurve> innerLoops { get; set; } = new();

  /// <summary>
  /// The units this object's coordinates are in.
  /// This should be one of <see cref="Units"/>
  /// </summary>
  public required string units { get; set; }

  /// <summary>
  /// Indication whether the region is just a geometry (false) or has a hatch pattern (true).
  /// </summary>
  public required bool hasHatchPattern { get; set; }

  /// <inheritdoc/>
  public double area { get; set; }

  /// <inheritdoc/>
  public required Box? bbox { get; set; }

  /// <inheritdoc/>
  [DetachProperty]
  public required List<Mesh> displayValue { get; set; }

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
        bbox = null,
        displayValue = new(),
        units = units,
      };
      return true;
    }

    return false;
  }
}
