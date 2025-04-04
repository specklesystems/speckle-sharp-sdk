using System.Diagnostics.CodeAnalysis;
using Speckle.Newtonsoft.Json;
using Speckle.Objects.Other;
using Speckle.Sdk.Common;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Geometry;

/// <summary>
/// A 3-dimensional point
/// </summary>
/// <remarks>
/// TODO: The Point class does not override the Equality operator, which means that there may be cases where `Equals` is used instead of `==`, as the comparison will be done by reference, not value.
/// </remarks>
[SpeckleType("Objects.Geometry.Point")]
public class Point : Base, ITransformable<Point>, IEquatable<Point>
{
  /// <inheritdoc/>
  public Point() { }

  /// <summary>
  /// Constructs a new <see cref="Point"/> from a set of coordinates and it's units.
  /// </summary>
  /// <param name="x">The x coordinate</param>
  /// <param name="y">The y coordinate</param>
  /// <param name="z">The z coordinate</param>
  /// <param name="units">The units of the point's coordinates. Defaults to Meters. </param>
  /// <param name="applicationId">The object's unique application ID</param>
  [SetsRequiredMembers]
  public Point(double x, double y, double z, string units, string? applicationId = null)
  {
    this.x = x;
    this.y = y;
    this.z = z;
    this.applicationId = applicationId;
    this.units = units;
  }

  /// <summary>
  /// The x coordinate of the point.
  /// </summary>
  public required double x { get; set; }

  /// <summary>
  /// The y coordinate of the point.
  /// </summary>
  public required double y { get; set; }

  /// <summary>
  /// The z coordinate of the point.
  /// </summary>
  public required double z { get; set; }

  /// <summary>
  /// The units this <see cref="Point"/> is in.
  /// This should be one of the units specified in <see cref="Units"/>
  /// </summary>
  public required string units { get; set; }

  /// <inheritdoc/>
  public bool TransformTo(Transform transform, out Point transformed)
  {
    var matrix = transform.matrix;

    var unitFactor = Units.GetConversionFactor(transform.units, units); // applied to translation vector
    var divisor = matrix.M41 + matrix.M42 + matrix.M43 + unitFactor * matrix.M44;
    var x = (this.x * matrix.M11 + this.y * matrix.M12 + this.z * matrix.M13 + unitFactor * matrix.M14) / divisor;
    var y = (this.x * matrix.M21 + this.y * matrix.M22 + this.z * matrix.M23 + unitFactor * matrix.M24) / divisor;
    var z = (this.x * matrix.M31 + this.y * matrix.M32 + this.z * matrix.M33 + unitFactor * matrix.M34) / divisor;

    transformed = new Point(x, y, z, units, applicationId);
    return true;
  }

  /// <inheritdoc/>
  public bool TransformTo(Transform transform, out ITransformable transformed)
  {
    var res = TransformTo(transform, out Point pt);
    transformed = pt;
    return res;
  }

  /// <summary>
  /// Returns the coordinates of this <see cref="Point"/> as a list of numbers
  /// </summary>
  /// <returns>A list of coordinates {x, y, z} </returns>
  public List<double> ToList()
  {
    return new List<double> { x, y, z };
  }

  public Vector ToVector()
  {
    return new Vector(x, y, z, units, applicationId);
  }

  /// <summary>
  /// Creates a new <see cref="Point"/> based on a list of coordinates and the unit they're drawn in.
  /// </summary>
  /// <param name="list">The list of coordinates {x, y, z}</param>
  /// <param name="units">The units the coordinates are in</param>
  /// <returns>A new <see cref="Point"/> with the provided coordinates.</returns>
  public static Point FromList(IList<double> list, string units)
  {
    return new Point(list[0], list[1], list[2], units);
  }

  /// <summary>
  /// Deconstructs a <see cref="Point"/> into it's coordinates and units
  /// </summary>
  /// <param name="x">The x coordinate</param>
  /// <param name="y">The y coordinate</param>
  /// <param name="z">The z coordinate</param>
  /// <param name="units">The units the point's coordinates are in.</param>
  public void Deconstruct(out double x, out double y, out double z, out string? units)
  {
    Deconstruct(out x, out y, out z);
    units = this.units;
  }

  /// <summary>
  /// Deconstructs a <see cref="Point"/> into it's coordinates and units
  /// </summary>
  /// <param name="x">The x coordinate</param>
  /// <param name="y">The y coordinate</param>
  /// <param name="z">The z coordinate</param>
  public void Deconstruct(out double x, out double y, out double z)
  {
    x = this.x;
    y = this.y;
    z = this.z;
  }

  public static Point operator +(Point point1, Point point2) =>
    new(point1.x + point2.x, point1.y + point2.y, point1.z + point2.z, point1.units);

  public static Point operator -(Point point1, Point point2) =>
    new(point1.x - point2.x, point1.y - point2.y, point1.z - point2.z, point1.units);

  public static Point operator *(Point point1, Point point2) =>
    new(point1.x * point2.x, point1.y * point2.y, point1.z * point2.z, point1.units);

  public static Point operator *(Point point, double val) =>
    new(point.x * val, point.y * val, point.z * val, point.units);

  public static Point operator /(Point point, double val) =>
    new(point.x / val, point.y / val, point.z / val, point.units);

  public static bool operator ==(Point? point1, Point? point2)
  {
    if (point1 is null && point2 is null)
    {
      return true;
    }
    else if (point1 is null || point2 is null)
    {
      return false;
    }

    return point1.units == point2.units && point1.x == point2.x && point1.y == point2.y && point1.z == point2.z;
  }

  public static bool operator !=(Point? point1, Point? point2) => !(point1 == point2);

  /// <summary>
  /// Computes a point equidistant from two points.
  /// </summary>
  /// <param name="point1">First point.</param>
  /// <param name="point2">Second point.</param>
  /// <returns>A point at the same distance from <paramref name="point1"/> and <paramref name="point2"/></returns>
  public static Point Midpoint(Point point1, Point point2)
  {
    return new Point(
      0.5 * (point1.x + point2.x),
      0.5 * (point1.y + point2.y),
      0.5 * (point1.z + point2.z),
      point1.units
    );
  }

  /// <summary>
  /// Computes the distance between two points
  /// </summary>
  /// <param name="point1">First point.</param>
  /// <param name="point2">Second point.</param>
  /// <returns>The distance from <paramref name="point1"/> to <paramref name="point2"/></returns>
  public static double Distance(Point point1, Point point2)
  {
    return Math.Sqrt(
      Math.Pow(point1.x - point2.x, 2) + Math.Pow(point1.y - point2.y, 2) + Math.Pow(point1.z - point2.z, 2)
    );
  }

  /// <summary>
  /// Computes the distance between two points.
  /// </summary>
  /// <param name="point">point for distance measurement</param>
  /// <returns>The length of the line between this and the other point</returns>
  public double DistanceTo(Point point)
  {
    return Math.Sqrt(Math.Pow(x - point.x, 2) + Math.Pow(y - point.y, 2) + Math.Pow(z - point.z, 2));
  }

  public bool Equals(Point? other) => this == other;

  public override bool Equals(object? obj)
  {
    if (ReferenceEquals(this, obj))
    {
      return true;
    }

    if (obj is Point p)
    {
      return this == p;
    }

    return false;
  }

  public override int GetHashCode()
  {
#if NETSTANDARD2_0
    return HashCode.Of(units).And(x).And(y).And(y);
#else
    return HashCode.Combine(units, x, y, z);
#endif
  }

  [Obsolete($"Use {nameof(Vector.ToPoint)}", true)]
  public Point(Vector _) { }

  /// <summary>
  /// Gets or sets the coordinates of the <see cref="Point"/>
  /// </summary>
  [JsonProperty(NullValueHandling = NullValueHandling.Ignore), Obsolete("Use x,y,z properties instead", true)]
  public List<double> value
  {
    get => null!;
    set
    {
      x = value[0];
      y = value[1];
      z = value.Count > 2 ? value[2] : 0;
    }
  }
}
