using Speckle.Objects.Primitive;
using Speckle.Sdk.Common;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Geometry;

[SpeckleType("Objects.Geometry.Ellipse")]
public class Ellipse : Base, ICurve, IHasArea
{
  /// <summary>
  /// Gets or sets the first radius of the <see cref="Ellipse"/>. This is usually the major radius.
  /// </summary>
  public required double firstRadius { get; set; }

  /// <summary>
  /// Gets or sets the second radius of the <see cref="Ellipse"/>. This is usually the minor radius.
  /// </summary>
  public required double secondRadius { get; set; }

  /// <summary>
  /// Gets or sets the plane to draw this ellipse in.
  /// </summary>
  public required Plane plane { get; set; }

  /// <summary>
  /// Gets or set the domain interval to trim this <see cref="Ellipse"/> with.
  /// </summary>
  public Interval? trimDomain { get; set; }

  /// <inheritdoc />
  public Box? bbox { get; set; }

  public required string units { get; set; }

  /// <summary>
  /// Gets or sets the domain interval for this <see cref="Ellipse"/>.
  /// </summary>
  public required Interval domain { get; set; }

  /// <inheritdoc />
  public double length { get; set; }

  //public Point center { get; set; }

  /// <inheritdoc />
  public double area { get; set; }

  public List<double> ToList()
  {
    var list = new List<double>();
    list.Add(firstRadius);
    list.Add(secondRadius);
    list.Add(domain.start);
    list.Add(domain.end);

    list.AddRange(plane.ToList());

    list.Add(Units.GetEncodingFromUnit(units));
    list.Insert(0, CurveTypeEncoding.Ellipse);
    list.Insert(0, list.Count);
    return list;
  }

  public static Ellipse FromList(List<double> list)
  {
    var ellipse = new Ellipse
    {
      firstRadius = list[2],
      secondRadius = list[3],
      domain = new Interval { start = list[4], end = list[5] },
      plane = Plane.FromList(list.GetRange(6, 13)),
      units = Units.GetUnitFromEncoding(list[list.Count - 1])
    };
    return ellipse;
  }
}
