using Speckle.Objects.Other;
using Speckle.Sdk;
using Speckle.Sdk.Common;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Geometry;

/// <summary>
/// A collection of points, with color and size support.
/// </summary>
[SpeckleType("Objects.Geometry.Pointcloud")]
public class Pointcloud : Base, IHasBoundingBox, ITransformable<Pointcloud>
{
  /// <summary>
  /// Gets or sets the list of points of this <see cref="Pointcloud"/>, stored as a flat list of coordinates [x1,y1,z1,x2,y2,...]
  /// </summary>
  [DetachProperty, Chunkable(31250)]
  public required List<double> points { get; set; }

  /// <summary>
  /// Gets or sets the list of colors of this <see cref="Pointcloud"/>'s points., stored as ARGB <see cref="int"/>s.
  /// </summary>
  [DetachProperty, Chunkable(62500)]
  public List<int> colors { get; set; } = new();

  /// <summary>
  /// Gets or sets the list of sizes of this <see cref="Pointcloud"/>'s points.
  /// </summary>
  [DetachProperty, Chunkable(62500)]
  public List<double> sizes { get; set; } = new();

  /// <summary>
  /// The unit's this <see cref="Pointcloud"/> is in.
  /// This should be one of <see cref="Units"/>
  /// </summary>
  public required string units { get; set; }

  /// <inheritdoc/>
  public Box? bbox { get; set; }

  /// <inheritdoc/>
  public Pointcloud TransformTo(Transform transform)
  {
    // transform points
    var originalPoints = GetPoints();
    var transformedPoints = new List<double>(originalPoints.Count * 3);
    foreach (var point in originalPoints)
    {
      var transformedPoint = point.TransformTo(transform);
      transformedPoints.Add(transformedPoint.x);
      transformedPoints.Add(transformedPoint.y);
      transformedPoints.Add(transformedPoint.z);
    }

    return new Pointcloud
    {
      units = units,
      points = transformedPoints,
      colors = colors,
      sizes = sizes,
      applicationId = applicationId,
    };
  }

  /// <returns><see cref="points"/> as list of <see cref="Point"/>s</returns>
  /// <exception cref="SpeckleException">when list is malformed</exception>
  public List<Point> GetPoints()
  {
    if (points.Count % 3 != 0)
    {
      throw new SpeckleException(
        $"{nameof(Pointcloud)}.{nameof(points)} list is malformed: expected length to be multiple of 3"
      );
    }

    var pts = new List<Point>(points.Count / 3);
    for (int i = 2; i < points.Count; i += 3)
    {
      pts.Add(new Point(points[i - 2], points[i - 1], points[i], units));
    }

    return pts;
  }
}
