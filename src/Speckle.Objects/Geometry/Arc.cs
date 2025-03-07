using Speckle.Newtonsoft.Json;
using Speckle.Objects.Other;
using Speckle.Objects.Primitive;
using Speckle.Sdk.Common;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Geometry;

/// <summary>
/// Represents a sub-curve of a three-dimensional circle.
/// </summary>
[SpeckleType("Objects.Geometry.Arc")]
public class Arc : Base, IHasBoundingBox, ICurve, ITransformable<Arc>
{
  /// <summary>
  /// Gets or sets the plane of the <see cref="Arc"/>.
  /// The plane origin is the <see cref="Arc"/> center.
  /// The plane normal indicates the handedness of the <see cref="Arc"/> such that direction from <see cref="startPoint"/> to <see cref="endPoint"/> is counterclockwise.
  /// </summary>
  public required Plane plane { get; set; }

  /// <summary>
  /// The start <see cref="Point"/> of the <see cref="Arc"/>
  /// </summary>
  public required Point startPoint { get; set; }

  /// <summary>
  /// Gets or sets the point at 0.5 length.
  /// </summary>
  public required Point midPoint { get; set; }

  /// <summary>
  /// The end <see cref="Point"/> of the <see cref="Arc"/>
  /// </summary>
  public required Point endPoint { get; set; }

  /// <summary>
  /// The radius of the <see cref="Arc"/>
  /// </summary>
  public double radius => Point.Distance(plane.origin, startPoint);

  /// <summary>
  /// OBSOLETE - This is just here for backwards compatibility.
  /// </summary>
  [JsonIgnore, Obsolete("start angle should be calculated from arc startpoint and plane if needed", true)]
  public double? startAngle { get; set; }

  /// <summary>
  /// OBSOLETE - This is just here for backwards compatibility.
  /// </summary>
  [JsonIgnore, Obsolete("end angle should be calculated from arc endpoint and plane if needed", true)]
  public double? endAngle { get; set; }

  /// <summary>
  /// OBSOLETE - This is just here for backwards compatibility.
  /// </summary>
  [JsonIgnore, Obsolete("Refer to measure instead", true)]
  public double angleRadians { get; set; }

  /// <summary>
  /// The measure of the <see cref="Arc"/> in radians.
  /// Calculated using the arc addition postulate using the <see cref="midPoint"/>.
  /// </summary>
  public double measure =>
    (2 * Math.Asin(Point.Distance(startPoint, midPoint) / (2 * radius)))
    + (2 * Math.Asin(Point.Distance(midPoint, endPoint) / (2 * radius)));

  /// <summary>
  /// The units this object was specified in.
  /// </summary>
  public required string units { get; set; }

  /// <inheritdoc/>
  public Interval domain { get; set; } = new() { start = 0, end = 0 };

  /// <summary>
  /// The length of the <see cref="Arc"/>
  /// </summary>
  public double length => radius * measure;

  /// <summary>
  /// OBSOLETE - This is just here for backwards compatibility.
  /// </summary>
  [JsonIgnore, Obsolete("Area property does not belong on an arc", true)]
  public double area { get; set; }

  /// <inheritdoc/>
  public Box? bbox { get; set; }

  /// <inheritdoc/>
  public bool TransformTo(Transform transform, out Arc transformed)
  {
    startPoint.TransformTo(transform, out Point transformedStartPoint);
    midPoint.TransformTo(transform, out Point transformedMidpoint);
    endPoint.TransformTo(transform, out Point transformedEndPoint);
    plane.TransformTo(transform, out Plane pln);
    Arc arc = new()
    {
      startPoint = transformedStartPoint,
      endPoint = transformedEndPoint,
      midPoint = transformedMidpoint,
      plane = pln,
      domain = domain,
      units = units,
    };
    transformed = arc;
    return true;
  }

  /// <inheritdoc/>
  public bool TransformTo(Transform transform, out ITransformable transformed)
  {
    var res = TransformTo(transform, out Arc arc);
    transformed = arc;
    return res;
  }

  /// <summary>
  /// Creates a flat list with the values of the <see cref="Arc"/>
  /// This is only used for serialisation purposes.
  /// </summary>
  /// <returns>A list of numbers representing the <see cref="Arc"/>'s value</returns>
  public List<double> ToList()
  {
    var list = new List<double>();
    list.Add(radius);
    list.Add(0); // Backwards compatibility: start angle
    list.Add(0); // Backwards compatibility: end angle
    list.Add(measure);
    list.Add(domain?.start ?? 0);
    list.Add(domain?.end ?? 0);
    list.AddRange(plane.ToList());
    list.AddRange(startPoint.ToList());
    list.AddRange(midPoint.ToList());
    list.AddRange(endPoint.ToList());
    list.Add(Units.GetEncodingFromUnit(units));
    list.Insert(0, CurveTypeEncoding.Arc);
    list.Insert(0, list.Count);
    return list;
  }

  /// <summary>
  /// Creates a new <see cref="Arc"/> instance based on a flat list of numerical values.
  /// This is only used for deserialisation purposes.
  /// </summary>
  /// <remarks>The input list should be the result of having called <see cref="Arc.ToList"/></remarks>
  /// <param name="list">A list of numbers</param>
  /// <returns>A new <see cref="Arc"/> with the values assigned from the list.</returns>
  public static Arc FromList(List<double> list)
  {
    string units = Units.GetUnitFromEncoding(list[^1]);
    Arc arc = new()
    {
      domain = new Interval { start = list[6], end = list[7] },
      units = units,
      plane = Plane.FromList(list.GetRange(8, 13)),
      startPoint = Point.FromList(list.GetRange(21, 3), units),
      midPoint = Point.FromList(list.GetRange(24, 3), units),
      endPoint = Point.FromList(list.GetRange(27, 3), units),
    };

    arc.plane.units = arc.units;
    return arc;
  }
}
