using Speckle.Newtonsoft.Json;
using Speckle.Objects.Primitive;
using Speckle.Sdk.Common;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Geometry;

/// <summary>
/// Represents a 3-dimensional box oriented on a plane.
/// </summary>
[SpeckleType("Objects.Geometry.Box")]
public class Box : Base, IHasVolume, IHasArea, IHasBoundingBox
{
  /// <summary>
  /// Gets or sets the plane that defines the orientation of the <see cref="Box"/>
  /// </summary>
  public required Plane basePlane { get; set; }

  /// <summary>
  /// Gets or sets the <see cref="Interval"/> that defines the min and max coordinate in the X direction
  /// </summary>
  public required Interval xSize { get; set; }

  /// <summary>
  /// Gets or sets the <see cref="Interval"/> that defines the min and max coordinate in the Y direction
  /// </summary>
  public required Interval ySize { get; set; }

  /// <summary>
  /// Gets or sets the <see cref="Interval"/> that defines the min and max coordinate in the Y direction
  /// </summary>
  public required Interval zSize { get; set; }

  /// <summary>
  /// The units this object's coordinates are in.
  /// </summary>
  /// <remarks>
  /// This should be one of <see cref="Units"/>
  /// </remarks>
  public required string units { get; set; }

  /// <inheritdoc/>
  public double area { get; set; } // TODO: compute

  [JsonIgnore, Obsolete("Boxs should not have a bounding box", true)]
  public Box? bbox { get; }

  /// <inheritdoc/>
  public double volume { get; set; } // TODO: compute
}
