using Speckle.Objects.Other;
using Speckle.Sdk.Common;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Geometry;

/// <summary>
/// A 3-dimensional Plane consisting of an origin <see cref="Point"/>, and 3 <see cref="Vector"/> as its X, Y and Z axis.
/// </summary>
[SpeckleType("Objects.Geometry.Plane")]
public class Plane : Base, ITransformable<Plane>
{
  /// <summary>
  /// The <see cref="Plane"/>s origin point.
  /// </summary>
  public required Point origin { get; set; }

  /// <summary>
  /// The <see cref="Plane"/>s Z axis.
  /// </summary>
  public required Vector normal { get; set; }

  /// <summary>
  /// The <see cref="Plane"/>s X axis.
  /// </summary>
  public required Vector xdir { get; set; }

  /// <summary>
  /// The <see cref="Plane"/>s Y axis.
  /// </summary>
  public required Vector ydir { get; set; }

  /// <summary>
  /// The unit's this <see cref="Plane"/> is in.
  /// This should be one of <see cref="Units"/>
  /// </summary>
  public required string units { get; set; }

  /// <inheritdoc/>
  public bool TransformTo(Transform transform, out Plane transformed)
  {
    origin.TransformTo(transform, out Point transformedOrigin);
    normal.TransformTo(transform, out Vector transformedNormal);
    xdir.TransformTo(transform, out Vector transformedXdir);
    ydir.TransformTo(transform, out Vector transformedYdir);
    transformed = new Plane
    {
      origin = transformedOrigin,
      normal = transformedNormal,
      xdir = transformedXdir,
      ydir = transformedYdir,
      applicationId = applicationId,
      units = units,
    };

    return true;
  }

  /// <inheritdoc/>
  public bool TransformTo(Transform transform, out ITransformable transformed)
  {
    var res = TransformTo(transform, out Plane plane);
    transformed = plane;
    return res;
  }

  /// <summary>
  /// Returns the values of this <see cref="Plane"/> as a list of numbers
  /// </summary>
  /// <returns>A list of values representing the Plane.</returns>
  public List<double> ToList()
  {
    var list = new List<double>();

    list.AddRange(origin.ToList());
    list.AddRange(normal.ToList());
    list.AddRange(xdir.ToList());
    list.AddRange(ydir.ToList());
    list.Add(Units.GetEncodingFromUnit(units));

    return list;
  }

  /// <summary>
  /// Creates a new <see cref="Plane"/> based on a list of values and the unit they're drawn in.
  /// </summary>
  /// <param name="list">The list of values representing this plane</param>
  /// <returns>A new <see cref="Plane"/> with the provided values.</returns>
  public static Plane FromList(IReadOnlyList<double> list)
  {
    var units = Units.GetUnitFromEncoding(list[^1]);
    var plane = new Plane
    {
      origin = new Point(list[0], list[1], list[2], units),
      normal = new Vector(list[3], list[4], list[5], units),
      xdir = new Vector(list[6], list[7], list[8], units),
      ydir = new Vector(list[9], list[10], list[11], units),
      units = units,
    };

    return plane;
  }
}
