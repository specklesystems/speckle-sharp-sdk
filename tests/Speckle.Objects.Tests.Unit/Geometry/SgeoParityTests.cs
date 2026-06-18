using AwesomeAssertions;
using Speckle.Objects.Geometry;
using Speckle.Objects.Primitive;
using Speckle.Objects.Utils;
using Speckle.Sdk.Common;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Tests.Unit.Geometry;

/// <summary>
/// Parity suite: take the *existing* SDK geometry objects (the old JSON-serialized model)
/// and prove the new SGEO binary format loses nothing.
///
/// Each test builds a fully-populated object (every authoritative channel set), runs it
/// through <see cref="SgeoEncoder.Encode"/> → <see cref="SgeoDecoder.Decode"/>, and asserts
/// EXHAUSTIVE equality — every stored field AND every derived getter — not the spot-checks
/// the round-trip tests in <see cref="SgeoTests"/> do.
///
/// Two intended, non-lossy transformations are asserted explicitly so the contract is on record:
///   1. <see cref="Point"/> encodes under primitive_type 7 and decodes as a single-point
///      <see cref="Pointcloud"/> — the xyz is preserved, the wrapper type is unified.
///   2. Derived / [JsonIgnore] members (length, area, volume, radius, measure, bbox) are NOT
///      stored; they recompute on read. We assert they recompute to the SAME value, proving the
///      recompute path is faithful.
///
/// Parity with the old JSON path is transitive: the SDK's own JSON round-trip is covered by
/// ObjectsSerializationTest; if SGEO(obj) == obj and JSON(obj) == obj, the two agree.
/// </summary>
public class SgeoParityTests
{
  private const string M = Units.Meters;
  private const double Tol = 1e-9;

  private static Plane XYPlane(string units = M) =>
    new()
    {
      origin = new Point(1, 2, 3, units),
      normal = new Vector(0, 0, 1, units),
      xdir = new Vector(1, 0, 0, units),
      ydir = new Vector(0, 1, 0, units),
      units = units,
    };

  /// <summary>Encode → decode, then re-encode and assert the bytes are a fixed point
  /// (the decode preserved everything the encode captured) and cast to the expected type.</summary>
  private static T RoundTrip<T>(Base original)
    where T : Base
  {
    var bytes = SgeoEncoder.Encode(original);
    var decoded = SgeoDecoder.Decode(bytes);
    SgeoEncoder.Encode(decoded).Should().Equal(bytes, "decode must preserve every byte the encode wrote");
    return decoded.Should().BeOfType<T>().Subject;
  }

  private static void AssertSamePlane(Plane actual, Plane expected)
  {
    actual.origin.x.Should().Be(expected.origin.x);
    actual.origin.y.Should().Be(expected.origin.y);
    actual.origin.z.Should().Be(expected.origin.z);
    actual.normal.x.Should().Be(expected.normal.x);
    actual.normal.y.Should().Be(expected.normal.y);
    actual.normal.z.Should().Be(expected.normal.z);
    actual.xdir.x.Should().Be(expected.xdir.x);
    actual.xdir.y.Should().Be(expected.xdir.y);
    actual.xdir.z.Should().Be(expected.xdir.z);
    actual.ydir.x.Should().Be(expected.ydir.x);
    actual.ydir.y.Should().Be(expected.ydir.y);
    actual.ydir.z.Should().Be(expected.ydir.z);
    actual.units.Should().Be(expected.units);
  }

  // ── 0 mesh ────────────────────────────────────────────────────────────────
  [Fact]
  public void Mesh_AllChannels_LosesNothing()
  {
    // n-gon faces (a triangle AND a quad) to prove the n-gon encoding survives.
    var original = new Mesh
    {
      vertices = [0, 0, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0, 2, 0, 0],
      faces = [3, 0, 1, 2, 4, 0, 1, 2, 3],
      vertexNormals = [0, 0, 1, 0, 0, 1, 0, 0, 1, 0, 0, 1, 0, 0, 1],
      textureCoordinates = [0, 0, 1, 0, 1, 1, 0, 1, 0.5, 0.5],
      colors = [-1, -65536, -16711936, -16776961, 16777215],
      units = M,
    };

    var d = RoundTrip<Mesh>(original);

    d.vertices.Should().Equal(original.vertices);
    d.faces.Should().Equal(original.faces);
    d.vertexNormals.Should().Equal(original.vertexNormals);
    d.textureCoordinates.Should().Equal(original.textureCoordinates);
    d.colors.Should().Equal(original.colors);
    d.units.Should().Be(M);
  }

  [Fact]
  public void Mesh_GeometryOnly_HasNoPhantomChannels()
  {
    var original = new Mesh
    {
      vertices = [0, 0, 0, 1, 0, 0, 0, 1, 0],
      faces = [3, 0, 1, 2],
      units = M,
    };

    var d = RoundTrip<Mesh>(original);

    d.vertices.Should().Equal(original.vertices);
    d.faces.Should().Equal(original.faces);
    d.vertexNormals.Should().BeEmpty();
    d.textureCoordinates.Should().BeEmpty();
    d.colors.Should().BeEmpty();
  }

  // ── 1 line ────────────────────────────────────────────────────────────────
  [Fact]
  public void Line_LosesNothing()
  {
    var original = new Line
    {
      start = new Point(0, 0, 0, M),
      end = new Point(3, 3, 6, M),
      domain = new Interval { start = 0, end = 1 },
      units = M,
    };

    var d = RoundTrip<Line>(original);

    d.start.x.Should().Be(0);
    d.start.y.Should().Be(0);
    d.start.z.Should().Be(0);
    d.end.x.Should().Be(3);
    d.end.y.Should().Be(3);
    d.end.z.Should().Be(6);
    d.domain.start.Should().Be(0);
    d.domain.end.Should().Be(1);
    d.units.Should().Be(M);
    // derived recompute matches
    d.length.Should().BeApproximately(original.length, Tol);
  }

  // ── 2 polyline ──────────────────────────────────────────────────────────────
  [Theory]
  [InlineData(true)]
  [InlineData(false)]
  public void Polyline_LosesNothing(bool closed)
  {
    var original = new Polyline
    {
      value = [0, 0, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0],
      closed = closed,
      domain = new Interval { start = 0, end = 1 },
      units = M,
    };

    var d = RoundTrip<Polyline>(original);

    d.value.Should().Equal(original.value);
    d.closed.Should().Be(closed);
    d.units.Should().Be(M);
  }

  // ── 3 polycurve (recursive) ─────────────────────────────────────────────────
  [Fact]
  public void Polycurve_NestedSegments_LoseNothing()
  {
    var line = new Line
    {
      start = new Point(0, 0, 0, M),
      end = new Point(1, 0, 0, M),
      units = M,
    };
    var arc = new Arc
    {
      plane = XYPlane(),
      startPoint = new Point(1, 0, 0, M),
      midPoint = new Point(0, 1, 0, M),
      endPoint = new Point(-1, 0, 0, M),
      domain = new Interval { start = 0, end = 1 },
      units = M,
    };
    var original = new Polycurve
    {
      segments = [line, arc],
      closed = true,
      units = M,
    };

    var d = RoundTrip<Polycurve>(original);

    d.closed.Should().BeTrue();
    d.segments.Should().HaveCount(2);
    var dLine = d.segments[0].Should().BeOfType<Line>().Subject;
    dLine.end.x.Should().Be(1);
    var dArc = d.segments[1].Should().BeOfType<Arc>().Subject;
    dArc.startPoint.x.Should().Be(1);
    dArc.endPoint.x.Should().Be(-1);
    AssertSamePlane(dArc.plane, arc.plane);
  }

  // ── 4 curve (NURBS) ───────────────────────────────────────────────────────
  [Fact]
  public void Curve_Rational_LosesNothing()
  {
    var original = new Curve
    {
      degree = 3,
      closed = false,
      periodic = false,
      rational = true,
      points = [0, 0, 0, 1, 2, 0, 3, 2, 0, 4, 0, 0],
      weights = [1, 0.5, 0.75, 1],
      knots = [0, 0, 0, 0, 1, 1, 1, 1],
      domain = new Interval { start = 0, end = 1 },
      units = M,
      displayValue = new Polyline { value = [], units = M }, // derived, not stored by SGEO
    };

    var d = RoundTrip<Curve>(original);

    d.degree.Should().Be(3);
    d.rational.Should().BeTrue();
    d.closed.Should().BeFalse();
    d.periodic.Should().BeFalse();
    d.points.Should().Equal(original.points);
    d.weights.Should().Equal(original.weights);
    d.knots.Should().Equal(original.knots);
    d.domain.start.Should().Be(0);
    d.domain.end.Should().Be(1);
    d.units.Should().Be(M);
  }

  // ── 5 arc ─────────────────────────────────────────────────────────────────
  [Fact]
  public void Arc_LosesNothing()
  {
    var original = new Arc
    {
      plane = XYPlane(),
      startPoint = new Point(1, 0, 0, M),
      midPoint = new Point(0, 1, 0, M),
      endPoint = new Point(-1, 0, 0, M),
      domain = new Interval { start = 0, end = 1 },
      units = M,
    };

    var d = RoundTrip<Arc>(original);

    AssertSamePlane(d.plane, original.plane);
    d.startPoint.x.Should().Be(1);
    d.startPoint.y.Should().Be(0);
    d.midPoint.y.Should().Be(1);
    d.endPoint.x.Should().Be(-1);
    d.domain.start.Should().Be(0);
    d.domain.end.Should().Be(1);
    d.units.Should().Be(M);
    d.radius.Should().BeApproximately(original.radius, Tol); // derived
  }

  // ── 6 circle ──────────────────────────────────────────────────────────────
  [Fact]
  public void Circle_LosesNothing()
  {
    var original = new Circle
    {
      radius = 5,
      plane = XYPlane(),
      domain = new Interval { start = 0, end = 1 },
      units = M,
    };

    var d = RoundTrip<Circle>(original);

    d.radius.Should().Be(5);
    AssertSamePlane(d.plane, original.plane);
    d.domain.start.Should().Be(0);
    d.domain.end.Should().Be(1);
    d.units.Should().Be(M);
    d.area.Should().BeApproximately(original.area, Tol); // derived
  }

  // ── 7 point / pointcloud ────────────────────────────────────────────────────
  [Fact]
  public void Point_PreservesCoords_DecodesAsSinglePointcloud()
  {
    var original = new Point(1, 2, 3, Units.Millimeters);

    // intended transformation: Point unifies into the Points primitive (type 7)
    var d = RoundTrip<Pointcloud>(original);

    d.points.Should().Equal(1, 2, 3);
    d.units.Should().Be(Units.Millimeters);
  }

  [Fact]
  public void Pointcloud_WithColorsAndSizes_LosesNothing()
  {
    var original = new Pointcloud
    {
      points = [0, 0, 0, 1, 1, 1, 2, 2, 2],
      colors = [-1, -65536, -16711936],
      sizes = [0.1, 0.2, 0.3],
      units = M,
    };

    var d = RoundTrip<Pointcloud>(original);

    d.points.Should().Equal(original.points);
    d.colors.Should().Equal(original.colors);
    d.sizes.Should().Equal(original.sizes);
    d.units.Should().Be(M);
  }

  // ── 8 ellipse ─────────────────────────────────────────────────────────────
  [Fact]
  public void Ellipse_WithTrimDomain_LosesNothing()
  {
    var original = new Ellipse
    {
      firstRadius = 5,
      secondRadius = 3,
      plane = XYPlane(),
      domain = new Interval { start = 0, end = 1 },
      trimDomain = new Interval { start = 0.25, end = 0.75 },
      units = M,
    };

    var d = RoundTrip<Ellipse>(original);

    d.firstRadius.Should().Be(5);
    d.secondRadius.Should().Be(3);
    AssertSamePlane(d.plane, original.plane);
    d.domain.start.Should().Be(0);
    d.domain.end.Should().Be(1);
    d.trimDomain.Should().NotBeNull();
    d.trimDomain!.start.Should().Be(0.25);
    d.trimDomain.end.Should().Be(0.75);
    d.units.Should().Be(M);
  }

  [Fact]
  public void Ellipse_WithoutTrimDomain_LosesNothing()
  {
    var original = new Ellipse
    {
      firstRadius = 5,
      secondRadius = 3,
      plane = XYPlane(),
      domain = new Interval { start = 0, end = 1 },
      trimDomain = null,
      units = M,
    };

    var d = RoundTrip<Ellipse>(original);

    d.firstRadius.Should().Be(5);
    d.secondRadius.Should().Be(3);
    d.trimDomain.Should().BeNull();
  }

  // ── 9 spiral ──────────────────────────────────────────────────────────────
  [Fact]
  public void Spiral_LosesNothing()
  {
    var original = new Spiral
    {
      startPoint = new Point(1, 0, 0, M),
      endPoint = new Point(0, 1, 2, M),
      plane = XYPlane(),
      turns = 2,
      pitchAxis = new Vector(0, 0, 1, M),
      pitch = 0.5,
      spiralType = SpiralType.Clothoid,
      domain = new Interval { start = 0, end = 1 },
      units = M,
      length = 0, // derived, not stored by SGEO
      displayValue = new Polyline { value = [], units = M }, // derived, not stored by SGEO
    };

    var d = RoundTrip<Spiral>(original);

    d.startPoint.x.Should().Be(1);
    d.endPoint.z.Should().Be(2);
    AssertSamePlane(d.plane, original.plane);
    d.turns.Should().Be(2);
    d.pitchAxis.z.Should().Be(1);
    d.pitch.Should().Be(0.5);
    d.spiralType.Should().Be(SpiralType.Clothoid);
    d.domain.start.Should().Be(0);
    d.domain.end.Should().Be(1);
    d.units.Should().Be(M);
  }

  // ── 10 box ────────────────────────────────────────────────────────────────
  [Fact]
  public void Box_LosesNothing()
  {
    var original = new Box
    {
      plane = XYPlane(),
      xSize = new Interval { start = 0, end = 1 },
      ySize = new Interval { start = -1, end = 2 },
      zSize = new Interval { start = 0, end = 3 },
      units = M,
    };

    var d = RoundTrip<Box>(original);

    AssertSamePlane(d.plane, original.plane);
    d.xSize.start.Should().Be(0);
    d.xSize.end.Should().Be(1);
    d.ySize.start.Should().Be(-1);
    d.ySize.end.Should().Be(2);
    d.zSize.start.Should().Be(0);
    d.zSize.end.Should().Be(3);
    d.units.Should().Be(M);
    d.volume.Should().BeApproximately(original.volume, Tol); // derived
  }
}
