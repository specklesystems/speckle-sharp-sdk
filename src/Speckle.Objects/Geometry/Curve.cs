using Speckle.Objects.Other;
using Speckle.Objects.Primitive;
using Speckle.Sdk;
using Speckle.Sdk.Common;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Geometry;

[SpeckleType("Objects.Geometry.Curve")]
public class Curve : Base, ICurve, IHasBoundingBox, IHasArea, ITransformable<Curve>, IDisplayValue<Polyline>
{
  public required int degree { get; set; }

  public required bool periodic { get; set; }

  /// <summary>
  /// "True" if weights differ, "False" if weights are the same.
  /// </summary>
  public required bool rational { get; set; }

  [DetachProperty, Chunkable(31250)]
  public required List<double> points { get; set; }

  /// <summary>
  /// Gets or sets the weights for this <see cref="Curve"/>. Use a default value of 1 for unweighted points.
  /// </summary>
  [DetachProperty, Chunkable(31250)]
  public required List<double> weights { get; set; }

  /// <summary>
  /// Gets or sets the knots for this <see cref="Curve"/>. Count should be equal to <see cref="points"/> count + <see cref="degree"/> + 1.
  /// </summary>
  [DetachProperty, Chunkable(31250)]
  public required List<double> knots { get; set; }

  public required bool closed { get; set; }

  /// <summary>
  /// The units this object was specified in.
  /// </summary>
  public required string units { get; set; }

  /// <inheritdoc/>
  public Interval domain { get; set; } = Interval.UnitInterval;

  /// <inheritdoc/>
  public double length { get; set; }

  /// <inheritdoc/>
  [DetachProperty]
  public required Polyline displayValue { get; set; }

  /// <inheritdoc/>
  public double area { get; set; }

  /// <inheritdoc/>
  public Box? bbox { get; set; }

  /// <inheritdoc/>
  public bool TransformTo(Transform transform, out Curve transformed)
  {
    // transform points
    var transformedPoints = new List<Point>();
    foreach (var point in GetPoints())
    {
      point.TransformTo(transform, out Point transformedPoint);
      transformedPoints.Add(transformedPoint);
    }

    var result = displayValue.TransformTo(transform, out ITransformable polyline);
    transformed = new Curve
    {
      degree = degree,
      periodic = periodic,
      rational = rational,
      points = transformedPoints.SelectMany(o => o.ToList()).ToList(),
      weights = weights,
      knots = knots,
      displayValue = (Polyline)polyline,
      closed = closed,
      units = units,
      applicationId = applicationId,
      domain = domain != null ? new Interval { start = domain.start, end = domain.end } : Interval.UnitInterval,
    };

    return result;
  }

  /// <inheritdoc/>
  public bool TransformTo(Transform transform, out ITransformable transformed)
  {
    var res = TransformTo(transform, out Curve curve);
    transformed = curve;
    return res;
  }

  /// <returns><see cref="points"/> as list of <see cref="Point"/>s</returns>
  /// <exception cref="SpeckleException">when list is malformed</exception>
  public List<Point> GetPoints()
  {
    if (points.Count % 3 != 0)
    {
      throw new SpeckleException(
        $"{nameof(Curve)}.{nameof(points)} list is malformed: expected length to be multiple of 3"
      );
    }

    var pts = new List<Point>(points.Count / 3);
    for (int i = 2; i < points.Count; i += 3)
    {
      pts.Add(new Point(points[i - 2], points[i - 1], points[i], units));
    }

    return pts;
  }

  /// <summary>
  /// Returns the values of this <see cref="Curve"/> as a list of numbers.
  /// </summary>
  /// <returns>A list of values representing the <see cref="Curve"/></returns>
  /// <remarks>
  /// This is currently only used for encoding optimization in curves in breps!
  /// </remarks>
  public List<double> ToList()
  {
    var list = new List<double>();
    var curve = this;
    list.Add(curve.degree); // 0
    list.Add(curve.periodic ? 1 : 0); // 1
    list.Add(curve.rational ? 1 : 0); // 2
    list.Add(curve.closed ? 1 : 0); // 3
    list.Add(curve.domain?.start ?? 0); // 4
    list.Add(curve.domain?.end ?? 1); // 5

    list.Add(curve.points.Count); // 6
    list.Add(curve.weights.Count); // 7
    list.Add(curve.knots.Count); // 8

    list.AddRange(curve.points); // 9 onwards
    list.AddRange(curve.weights);
    list.AddRange(curve.knots);

    list.Add(Units.GetEncodingFromUnit(units));
    list.Insert(0, CurveTypeEncoding.Curve);
    list.Insert(0, list.Count);
    return list;
  }

  /// <summary>
  /// Creates a new <see cref="Curve"/> based on a list of coordinates and the unit they're drawn in.
  /// </summary>
  /// <param name="list">The list of values representing this <see cref="Curve"/></param>
  /// <returns>A new <see cref="Curve"/> with the provided values.</returns>
  /// <remarks>
  /// This is currently being used only for deserialization of Brep curves!
  /// </remarks>
  public static Curve FromList(List<double> list)
  {
    if ((int)list[0] != list.Count - 1)
    {
      throw new ArgumentException($"Incorrect length. Expected {list[0]}, got {list.Count}", nameof(list));
    }

    if (list[1] != CurveTypeEncoding.Curve)
    {
      throw new ArgumentException($"Wrong curve type. Expected {CurveTypeEncoding.Curve}, got {list[1]}", nameof(list));
    }

    var pointsCount = (int)list[8];
    var weightsCount = (int)list[9];
    var knotsCount = (int)list[10];

    string units = Units.GetUnitFromEncoding(list[^1]);
    var curve = new Curve
    {
      degree = (int)list[2],
      periodic = (int)list[3] == 1,
      rational = (int)list[4] == 1,
      closed = (int)list[5] == 1,
      domain = new Interval { start = list[6], end = list[7] },
      displayValue = new Polyline { value = new(), units = units }, // this is unique to breps, so we do not create curves with null displayValues
      points = list.GetRange(11, pointsCount),
      weights = list.GetRange(11 + pointsCount, weightsCount),
      knots = list.GetRange(11 + pointsCount + weightsCount, knotsCount),
      units = units,
    };

    return curve;
  }
}
