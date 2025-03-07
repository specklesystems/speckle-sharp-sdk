using Speckle.Objects.Other;
using Speckle.Objects.Primitive;
using Speckle.Sdk.Common;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Geometry;

/// <summary>
/// A curve that is comprised of multiple curves connected.
/// </summary>
[SpeckleType("Objects.Geometry.Polycurve")]
public class Polycurve : Base, ICurve, IHasArea, IHasBoundingBox, ITransformable
{
  /// <summary>
  /// Gets or sets the list of segments that comprise this <see cref="Polycurve"/>
  /// </summary>
  public required List<ICurve> segments { get; set; }

  /// <summary>
  /// Gets or sets a Boolean value indicating if the <see cref="Polycurve"/> is closed
  /// (i.e. The start point of the first segment and the end point of the last segment coincide.)
  /// </summary>
  public bool closed { get; set; }

  /// <summary>
  /// The unit's this <see cref="Polycurve"/> is in.
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
  public bool TransformTo(Transform transform, out ITransformable polycurve)
  {
    // transform segments
    var success = true;
    var transformed = new List<ICurve>();
    foreach (var curve in segments)
    {
      if (curve is ITransformable c)
      {
        c.TransformTo(transform, out ITransformable tc);
        transformed.Add((ICurve)tc);
      }
      else
      {
        success = false;
      }
    }

    polycurve = new Polycurve
    {
      segments = transformed,
      applicationId = applicationId,
      closed = closed,
      units = units,
    };

    return success;
  }

  /// <summary>
  /// Constructs a new <see cref="Polycurve"/> instance from an existing <see cref="Polyline"/> curve.
  /// </summary>
  /// <param name="polyline">The polyline to be used when constructing the <see cref="Polycurve"/></param>
  /// <returns>A <see cref="Polycurve"/> with the same shape as the provided polyline.</returns>
  public static implicit operator Polycurve(Polyline polyline)
  {
    Polycurve polycurve = new()
    {
      segments = new(),
      units = polyline.units,
      area = polyline.area,
      domain = polyline.domain,
      closed = polyline.closed,
      bbox = polyline.bbox,
      length = polyline.length,
    };

    var points = polyline.GetPoints();
    for (var i = 0; i < points.Count - 1; i++)
    {
      var line = new Line
      {
        start = points[i],
        end = points[i + 1],
        units = polyline.units,
      };
      polycurve.segments.Add(line);
    }

    if (polyline.closed)
    {
      var line = new Line
      {
        start = points[^1],
        end = points[0],
        units = polyline.units,
      };
      polycurve.segments.Add(line);
    }

    return polycurve;
  }

  /// <summary>
  /// Returns the values of this <see cref="Polycurve"/> as a list of numbers
  /// </summary>
  /// <returns>A list of values representing the polycurve.</returns>
  public List<double> ToList()
  {
    var list = new List<double>();
    list.Add(closed ? 1 : 0);
    list.Add(domain.start);
    list.Add(domain.end);

    var crvs = CurveArrayEncodingExtensions.ToArray(segments);
    list.Add(crvs.Count);
    list.AddRange(crvs);

    list.Add(Units.GetEncodingFromUnit(units));
    list.Insert(0, CurveTypeEncoding.PolyCurve);
    list.Insert(0, list.Count);

    return list;
  }

  /// <summary>
  /// Creates a new <see cref="Polycurve"/> based on a list of coordinates and the unit they're drawn in.
  /// </summary>
  /// <param name="list">The list of values representing this polycurve</param>
  /// <returns>A new <see cref="Polycurve"/> with the provided values.</returns>
  public static Polycurve FromList(List<double> list)
  {
    var temp = list.GetRange(6, (int)list[5]);
    var polycurve = new Polycurve
    {
      segments = CurveArrayEncodingExtensions.FromArray(temp),
      closed = (int)list[2] == 1,
      domain = new Interval { start = list[3], end = list[4] },
      units = Units.GetUnitFromEncoding(list[^1]),
    };

    return polycurve;
  }
}
