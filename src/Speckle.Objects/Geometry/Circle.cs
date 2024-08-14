using Speckle.Objects.Primitive;
using Speckle.Sdk.Common;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Geometry;

/// <summary>
/// Represents a circular curve based on a base <see cref="Plane"/> and a <see cref="double"/> as radius.
/// </summary>
[SpeckleType("Objects.Geometry.Circle")]
public class Circle : Base, ICurve, IHasArea, IHasBoundingBox
{
  /// <summary>
  /// The radius of the circle
  /// </summary>
  public required double radius { get; set; }

  /// <summary>
  /// The <see cref="Plane"/> the circle lies in.
  /// </summary>
  public required Plane plane { get; set; }

  /// <summary>
  /// The units this object was modeled in.
  /// </summary>
  public required string units { get; set; }

  /// <inheritdoc/>
  public Interval domain { get; set; } = Interval.UnitInterval; //TODO: should this be 0 - 2pi

  /// <inheritdoc/>
  public double length { get; set; } //TODO: should this be calculated

  //public Point center { get; set; }

  /// <inheritdoc/>
  public double area { get; set; }

  /// <inheritdoc/>
  public Box? bbox { get; set; }

  /// <summary>
  /// Returns the coordinates of this <see cref="Circle"/> as a list of numbers
  /// </summary>
  /// <returns>A list of values representing the <see cref="Circle"/></returns>
  public List<double> ToList()
  {
    var list = new List<double>();

    list.Add(radius);
    list.Add(domain.start);
    list.Add(domain.end);
    list.AddRange(plane.ToList());

    list.Add(Units.GetEncodingFromUnit(units));
    list.Insert(0, CurveTypeEncoding.Circle);
    list.Insert(0, list.Count);
    return list;
  }

  /// <summary>
  /// Creates a new <see cref="Circle"/> based on a list of coordinates and the unit they're drawn in.
  /// </summary>
  /// <param name="list">The list of values representing this <see cref="Circle"/></param>
  /// <returns>A new <see cref="Circle"/> with the provided values.</returns>
  public static Circle FromList(List<double> list)
  {
    var circle = new Circle
    {
      radius = list[2],
      domain = new Interval { start = list[3], end = list[4] },
      plane = Plane.FromList(list.GetRange(5, 13)),
      units = Units.GetUnitFromEncoding(list[list.Count - 1])
    };

    return circle;
  }
}
