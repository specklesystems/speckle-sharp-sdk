using System.Diagnostics.CodeAnalysis;
using Speckle.Objects.Other;
using Speckle.Objects.Primitive;
using Speckle.Sdk.Common;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Geometry;

/// <summary>
/// A Surface in NURBS form.
/// </summary>
[SpeckleType("Objects.Geometry.Surface")]
public class Surface : Base, IHasBoundingBox, IHasArea, ITransformable<Surface>
{
  [Obsolete("Constructor should only be used by serializer, use one of the other constructors instead")]
  public Surface()
  {
    pointData = [];
  }

  public Surface(List<List<ControlPoint>> controlPoints)
  {
    SetControlPoints(controlPoints);
  }

  public Surface(IList<double> pointData, int countU, int countV)
  {
    this.pointData = pointData;
    this.countU = countU;
    this.countV = countV;
  }

  /// <summary>
  /// The degree of the surface in the U direction
  /// </summary>
  public required int degreeU { get; set; }

  /// <summary>
  /// The degree of the surface in the V direction
  /// </summary>
  public required int degreeV { get; set; }

  /// <summary>
  /// Determines if the <see cref="Surface"/> is rational.
  /// </summary>
  public required bool rational { get; set; }

  /// <summary>
  /// The raw data of the surface's control points. Use <see cref="GetControlPoints"/> or <see cref="SetControlPoints"/> instead of accessing this directly.
  /// </summary>
  public IList<double> pointData { get; set; }

  /// <summary>
  /// The number of control points in the U direction
  /// </summary>
  public int countU { get; set; }

  /// <summary>
  /// The number of control points in the V direction
  /// </summary>
  public int countV { get; set; }

  /// <summary>
  /// The knot vector in the U direction
  /// </summary>
  public required List<double> knotsU { get; set; }

  /// <summary>
  /// The knot vector in the V direction
  /// </summary>
  public required List<double> knotsV { get; set; }

  /// <summary>
  /// The surface's domain in the U direction
  /// </summary>
  public required Interval domainU { get; set; }

  /// <summary>
  /// The surface's domain in the V direction
  /// </summary>
  public required Interval domainV { get; set; }

  /// <summary>
  /// Determines if a surface is closed around the <see cref="domainU"/>.
  /// </summary>
  public required bool closedU { get; set; }

  /// <summary>
  /// Determines if a surface is closed around the <see cref="domainV"/>
  /// </summary>
  public required bool closedV { get; set; }

  /// <summary>
  /// The unit's this <see cref="Surface"/> is in.
  /// This should be one of <see cref="Units"/>
  /// </summary>
  public required string units { get; set; }

  /// <inheritdoc/>
  public double area { get; set; }

  /// <inheritdoc/>
  public Box? bbox { get; set; }

  /// <inheritdoc/>
  public Surface TransformTo(Transform transform)
  {
    var ptMatrix = GetControlPoints();
    foreach (var ctrlPts in ptMatrix)
    {
      for (int i = 0; i < ctrlPts.Count; i++)
      {
        ITransformable<ControlPoint> pt = ctrlPts[i];
        var tPt = pt.TransformTo(transform);
        ctrlPts[i] = tPt;
      }
    }

    return new Surface(ptMatrix)
    {
      degreeU = degreeU,
      degreeV = degreeV,
      countU = countU,
      countV = countV,
      rational = rational,
      closedU = closedU,
      closedV = closedV,
      domainU = domainU,
      domainV = domainV,
      knotsU = knotsU,
      knotsV = knotsV,
      units = units,
    };
  }

  /// <summary>
  /// Gets the control points of this s<see cref="Surface"/>
  /// </summary>
  /// <returns>A 2-dimensional array representing this <see cref="Surface"/>s control points.</returns>
  /// <remarks>The ControlPoints will be ordered following directions "[u][v]"</remarks>

  public List<List<ControlPoint>> GetControlPoints()
  {
    var matrix = new List<List<ControlPoint>>();
    for (var i = 0; i < countU; i++)
    {
      matrix.Add(new List<ControlPoint>());
    }

    for (var i = 0; i < pointData.Count; i += 4)
    {
      var uIndex = i / (countV * 4);
      matrix[uIndex].Add(new ControlPoint(pointData[i], pointData[i + 1], pointData[i + 2], pointData[i + 3], units));
    }

    return matrix;
  }

  /// <summary>
  /// Sets the control points of this <see cref="Surface"/>.
  /// </summary>
  /// <param name="value">A 2-dimensional array of <see cref="ControlPoint"/> instances.</param>
  /// <remarks>The <paramref name="value"/> must be ordered following directions "[u][v]"</remarks>
  [MemberNotNull(nameof(pointData))]
  [MemberNotNull(nameof(countU))]
  [MemberNotNull(nameof(countV))]
  public void SetControlPoints(List<List<ControlPoint>> value)
  {
    List<double> data = new();
    countU = value.Count;
    countV = value[0].Count;
    value.ForEach(row =>
      row.ForEach(pt =>
      {
        data.Add(pt.x);
        data.Add(pt.y);
        data.Add(pt.z);
        data.Add(pt.weight);
      })
    );
    pointData = data;
  }

  /// <summary>
  /// Returns the coordinates of this <see cref="Surface"/> as a list of numbers
  /// </summary>
  /// <returns>A list of values representing the surface</returns>
  public List<double> ToList()
  {
    var list = new List<double>();
    list.Add(degreeU);
    list.Add(degreeV);
    list.Add(countU);
    list.Add(countV);
    list.Add(rational ? 1 : 0);
    list.Add(closedU ? 1 : 0);
    list.Add(closedV ? 1 : 0);
    list.Add(domainU.start); // 7
    list.Add(domainU.end);
    list.Add(domainV.start);
    list.Add(domainV.end); // [0] 10

    list.Add(pointData.Count); // 11
    list.Add(knotsU.Count); // 12
    list.Add(knotsV.Count); // 13

    list.AddRange(pointData);
    list.AddRange(knotsU);
    list.AddRange(knotsV);

    list.Add(Units.GetEncodingFromUnit(units));
    list.Insert(0, list.Count);

    return list;
  }

  /// <summary>
  /// Creates a new <see cref="Surface"/> based on a list of coordinates and the unit they're drawn in.
  /// </summary>
  /// <param name="list">The list of values representing this surface</param>
  /// <returns>A new <see cref="Surface"/> with the provided values.</returns>
  public static Surface FromList(List<double> list)
  {
    var pointCount = (int)list[11];
    var knotsUCount = (int)list[12];
    var knotsVCount = (int)list[13];
    var countU = (int)list[2];
    var countV = (int)list[3];

    var pointData = list.GetRange(14, pointCount);
    var u = list[^1];

    return new Surface(pointData, countU, countV)
    {
      degreeU = (int)list[0],
      degreeV = (int)list[1],
      rational = list[4] == 1,
      closedU = list[5] == 1,
      closedV = list[6] == 1,
      domainU = new Interval { start = list[7], end = list[8] },
      domainV = new Interval { start = list[9], end = list[10] },
      knotsU = list.GetRange(14 + pointCount, knotsUCount),
      knotsV = list.GetRange(14 + pointCount + knotsUCount, knotsVCount),
      units = Units.GetUnitFromEncoding(u),
    };
  }
}
