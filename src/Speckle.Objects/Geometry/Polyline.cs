using Speckle.Objects.Other;
using Speckle.Objects.Primitive;
using Speckle.Sdk;
using Speckle.Sdk.Common;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Geometry;

/// <summary>
/// A polyline curve, defined by a set of vertices.
/// </summary>
[SpeckleType("Objects.Geometry.Polyline")]
public class Polyline : Base, ICurve, IHasArea, IHasBoundingBox, ITransformable<Polyline>
{
  /// <summary>
  /// Gets or sets the raw coordinates that define this polyline. Use GetPoints instead to access this data as <see cref="Point"/> instances instead.
  /// </summary>
  [DetachProperty, Chunkable(31250)]
  public required List<double> value { get; set; }

  /// <remarks>
  /// If true, do not add the last point to the value list. Polyline first and last points should be unique.
  /// </remarks>
  public bool closed { get; set; }

  /// <summary>
  /// The unit's this <see cref="Polyline"/> is in.
  /// This should be one of <see cref="Units"/>
  /// </summary>
  public required string units { get; set; }

  /// <summary>
  /// The internal domain of this curve.
  /// </summary>
  public Interval domain { get; set; } = Interval.UnitInterval;

  /// <inheritdoc/>
  public double length { get; set; }

  /// <inheritdoc/>
  public double area { get; set; }

  /// <inheritdoc/>
  public Box? bbox { get; set; }

  /// <inheritdoc/>
  public Polyline TransformTo(Transform transform)
  {
    // transform points
    var originalPoints = GetPoints();
    var transformedPoints = new List<double>(originalPoints.Count * 3);
    foreach (var point in originalPoints)
    {
      Point transformedPoint = point.TransformTo(transform);
      transformedPoints.Add(transformedPoint.x);
      transformedPoints.Add(transformedPoint.y);
      transformedPoints.Add(transformedPoint.z);
    }

    return new Polyline
    {
      value = transformedPoints,
      closed = closed,
      applicationId = applicationId,
      units = units,
    };
  }

  ///<remarks>This function may be suboptimal for performance for polylines with many points</remarks>
  /// <returns><see cref="value"/> as List of <see cref="Point"/>s</returns>
  /// <exception cref="SpeckleException">when list is malformed</exception>
  public List<Point> GetPoints()
  {
    if (value.Count % 3 != 0)
    {
      throw new SpeckleException(
        $"{nameof(Polyline)}.{nameof(value)} list is malformed: expected length to be multiple of 3"
      );
    }

    var pts = new List<Point>(value.Count / 3);
    for (int i = 2; i < value.Count; i += 3)
    {
      pts.Add(new Point(value[i - 2], value[i - 1], value[i], units));
    }

    return pts;
  }

  /// <summary>
  /// Returns the values of this <see cref="Polyline"/> as a list of numbers
  /// </summary>
  /// <returns>A list of values representing the polyline.</returns>
  public List<double> ToList()
  {
    var list = new List<double>();
    list.Add(closed ? 1 : 0); // 2
    list.Add(domain?.start ?? 0); // 3
    list.Add(domain?.end ?? 1); // 4
    list.Add(value.Count); // 5
    list.AddRange(value); // 6 onwards

    list.Add(Units.GetEncodingFromUnit(units));
    list.Insert(0, CurveTypeEncoding.Polyline); // 1
    list.Insert(0, list.Count); // 0
    return list;
  }

  /// <summary>
  /// Creates a new <see cref="Polyline"/> based on a list of coordinates and the unit they're drawn in.
  /// </summary>
  /// <param name="list">The list of values representing this polyline</param>
  /// <returns>A new <see cref="Polyline"/> with the provided values.</returns>
  public static Polyline FromList(List<double> list)
  {
    int pointCount = (int)list[5];
    return new()
    {
      closed = (int)list[2] == 1,
      domain = new Interval { start = list[3], end = list[4] },
      value = list.GetRange(6, pointCount),
      units = Units.GetUnitFromEncoding(list[^1]),
    };
  }
}
