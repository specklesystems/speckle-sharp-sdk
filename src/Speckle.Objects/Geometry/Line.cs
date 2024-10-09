using System.Diagnostics.CodeAnalysis;
using Speckle.Newtonsoft.Json;
using Speckle.Objects.Other;
using Speckle.Objects.Primitive;
using Speckle.Sdk.Common;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Geometry;

[SpeckleType("Objects.Geometry.Line")]
public class Line : Base, ICurve, IHasBoundingBox, ITransformable<Line>
{
  public Line() { }

  /// <param name="coordinates"></param>
  /// <param name="units"></param>
  /// <param name="applicationId"></param>
  /// <exception cref="ArgumentException"><paramref name="coordinates"/> must have a length of 6</exception>
  [SetsRequiredMembers]
  public Line(IList<double> coordinates, string units, string? applicationId = null)
  {
    if (coordinates.Count < 6)
    {
      throw new ArgumentException("Line from coordinate array requires 6 coordinates.", nameof(coordinates));
    }

    start = new Point(coordinates[0], coordinates[1], coordinates[2], units, applicationId);
    end = new Point(coordinates[3], coordinates[4], coordinates[5], units, applicationId);
    this.units = units;
    this.applicationId = applicationId;
  }

  public double area { get; set; }

  public required string units { get; set; }

  public required Point start { get; set; }
  public required Point end { get; set; }

  public Interval domain { get; set; } = Interval.UnitInterval;
  public double length => Point.Distance(start, end);

  public Box? bbox { get; set; }

  public bool TransformTo(Transform transform, out Line transformed)
  {
    start.TransformTo(transform, out Point transformedStart);
    end.TransformTo(transform, out Point transformedEnd);
    transformed = new Line
    {
      start = transformedStart,
      end = transformedEnd,
      applicationId = applicationId,
      units = units,
      domain = new() { start = domain.start, end = domain.end },
    };
    return true;
  }

  public bool TransformTo(Transform transform, out ITransformable transformed)
  {
    var res = TransformTo(transform, out Line line);
    transformed = line;
    return res;
  }

  public List<double> ToList()
  {
    var list = new List<double>();
    list.AddRange(start.ToList());
    list.AddRange(end.ToList());
    list.Add(domain?.start ?? 0);
    list.Add(domain?.end ?? 1);
    list.Add(Units.GetEncodingFromUnit(units));
    list.Insert(0, CurveTypeEncoding.Line);
    list.Insert(0, list.Count);
    return list;
  }

  public static Line FromList(IReadOnlyList<double> list)
  {
    var units = Units.GetUnitFromEncoding(list[^1]);
    var startPt = new Point(list[2], list[3], list[4], units);
    var endPt = new Point(list[5], list[6], list[7], units);
    var line = new Line
    {
      start = startPt,
      end = endPt,
      units = units,
      domain = new Interval { start = list[8], end = list[9] },
    };
    return line;
  }

  /// <summary>
  /// OBSOLETE - This is just here for backwards compatibility.
  /// You should not use this for anything. Access coordinates using start and end point.
  /// </summary>
  [
    JsonProperty(NullValueHandling = NullValueHandling.Ignore),
    Obsolete("Access coordinates using start and end point", true)
  ]
  public List<double>? value
  {
    get => null;
    set
    {
      if (value == null)
      {
        return;
      }

      start = new Point(value[0], value[1], value[2], Units.Meters);
      end = new Point(value[3], value[4], value[5], Units.Meters);
    }
  }
}
