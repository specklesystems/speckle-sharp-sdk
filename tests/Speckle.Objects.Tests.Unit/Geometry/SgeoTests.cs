using System.Text;
using AwesomeAssertions;
using Speckle.Objects.Geometry;
using Speckle.Objects.Primitive;
using Speckle.Objects.Utils;
using Speckle.Sdk.Common;

namespace Speckle.Objects.Tests.Unit.Geometry;

public class SgeoTests
{
  private static Plane XYPlane(string units) =>
    new()
    {
      origin = new Point(0, 0, 0, units),
      normal = new Vector(0, 0, 1, units),
      xdir = new Vector(1, 0, 0, units),
      ydir = new Vector(0, 1, 0, units),
      units = units,
    };

  private static string Magic(byte[] bytes) => Encoding.ASCII.GetString(bytes, 0, 4);

  // ── header / golden vectors ──────────────────────────────────────────────

  [Fact]
  public void Line_MatchesBoardGoldenVector()
  {
    var line = new Line
    {
      start = new Point(0, 0, 0, Units.Meters),
      end = new Point(3, 3, 6, Units.Meters),
      units = Units.Meters,
      domain = Interval.UnitInterval,
    };

    var bytes = SgeoEncoder.Encode(line);

    // 16-byte header + 8 doubles (domain + start + end) = 80 bytes
    bytes.Length.Should().Be(80);
    Magic(bytes).Should().Be("SGEO");

    var header = SgeoDecoder.ReadHeader(bytes);
    header.Version.Should().Be(1);
    header.PrimitiveType.Should().Be(SgeoPrimitiveType.Line);
    header.UnitsCode.Should().Be((ushort)Units.GetEncodingFromUnit(Units.Meters)); // meters = 3
    header.Flags.Should().Be(SgeoFlags.None);

    var decoded = (Line)SgeoDecoder.Decode(bytes);
    decoded.start.x.Should().Be(0);
    decoded.end.x.Should().Be(3);
    decoded.end.y.Should().Be(3);
    decoded.end.z.Should().Be(6);
    decoded.domain.start.Should().Be(0);
    decoded.domain.end.Should().Be(1);
    decoded.units.Should().Be(Units.Meters);
  }

  [Fact]
  public void Circle_MatchesBoardGoldenVector()
  {
    var circle = new Circle
    {
      radius = 5,
      plane = XYPlane(Units.Meters),
      units = Units.Meters,
      domain = Interval.UnitInterval,
    };

    var bytes = SgeoEncoder.Encode(circle);

    // 16-byte header + 15 doubles (radius + domain + plane) = 136 bytes
    bytes.Length.Should().Be(136);
    var header = SgeoDecoder.ReadHeader(bytes);
    header.PrimitiveType.Should().Be(SgeoPrimitiveType.Circle);

    var decoded = (Circle)SgeoDecoder.Decode(bytes);
    decoded.radius.Should().Be(5);
    decoded.plane.normal.z.Should().Be(1);
    decoded.plane.xdir.x.Should().Be(1);
    decoded.units.Should().Be(Units.Meters);
  }

  [Fact]
  public void Point_EncodesAsSinglePointBody()
  {
    var point = new Point(1, 2, 3, Units.Millimeters);

    var bytes = SgeoEncoder.Encode(point);

    // 16-byte header + (count u32 + reserved u32) + 3 doubles = 48 bytes
    bytes.Length.Should().Be(48);
    var header = SgeoDecoder.ReadHeader(bytes);
    header.PrimitiveType.Should().Be(SgeoPrimitiveType.Points);
    header.UnitsCode.Should().Be((ushort)Units.GetEncodingFromUnit(Units.Millimeters)); // mm = 1

    // A single Point decodes to a one-point Pointcloud (documented behaviour).
    var decoded = (Pointcloud)SgeoDecoder.Decode(bytes);
    decoded.points.Should().Equal(1, 2, 3);
  }

  [Fact]
  public void Encode_IsDeterministic()
  {
    var circle = new Circle
    {
      radius = 5,
      plane = XYPlane(Units.Meters),
      units = Units.Meters,
    };
    SgeoEncoder.Encode(circle).Should().Equal(SgeoEncoder.Encode(circle));
  }

  // ── round-trips ───────────────────────────────────────────────────────────

  [Fact]
  public void Mesh_RoundTripsBitExact()
  {
    var mesh = new Mesh
    {
      vertices = [0, 0, 0, 1, 0, 0, 0, 1, 0, 1, 1, 0],
      faces = [3, 0, 1, 2, 3, 1, 3, 2],
      units = Units.Meters,
    };

    var bytes = SgeoEncoder.Encode(mesh);
    SgeoDecoder.ReadHeader(bytes).PrimitiveType.Should().Be(SgeoPrimitiveType.Mesh);

    var decoded = (Mesh)SgeoDecoder.Decode(bytes);
    decoded.vertices.Should().Equal(mesh.vertices);
    decoded.faces.Should().Equal(mesh.faces);
    decoded.vertexNormals.Should().BeEmpty();
    decoded.units.Should().Be(Units.Meters);
  }

  [Fact]
  public void Mesh_WithNormalsUvsColors_RoundTrips()
  {
    var mesh = new Mesh
    {
      vertices = [0, 0, 0, 1, 0, 0, 0, 1, 0],
      faces = [3, 0, 1, 2],
      vertexNormals = [0, 0, 1, 0, 0, 1, 0, 0, 1],
      textureCoordinates = [0, 0, 1, 0, 0, 1],
      colors = [unchecked((int)0xFFFF0000), unchecked((int)0xFF00FF00), unchecked((int)0xFF0000FF)],
      units = Units.Meters,
    };

    var bytes = SgeoEncoder.Encode(mesh);
    var header = SgeoDecoder.ReadHeader(bytes);
    header.Flags.Should().HaveFlag(SgeoFlags.HasNormals);
    header.Flags.Should().HaveFlag(SgeoFlags.HasUvs);
    header.Flags.Should().HaveFlag(SgeoFlags.HasColors);

    var decoded = (Mesh)SgeoDecoder.Decode(bytes);
    decoded.vertexNormals.Should().Equal(mesh.vertexNormals);
    decoded.textureCoordinates.Should().Equal(mesh.textureCoordinates);
    decoded.colors.Should().Equal(mesh.colors);
  }

  [Fact]
  public void Polyline_Closed_RoundTrips()
  {
    var polyline = new Polyline
    {
      value = [0, 0, 0, 1, 0, 0, 1, 1, 0],
      closed = true,
      units = Units.Meters,
    };

    var bytes = SgeoEncoder.Encode(polyline);
    SgeoDecoder.ReadHeader(bytes).Flags.Should().HaveFlag(SgeoFlags.Closed);

    var decoded = (Polyline)SgeoDecoder.Decode(bytes);
    decoded.value.Should().Equal(polyline.value);
    decoded.closed.Should().BeTrue();
  }

  [Fact]
  public void Arc_RoundTrips()
  {
    var arc = new Arc
    {
      plane = XYPlane(Units.Meters),
      startPoint = new Point(1, 0, 0, Units.Meters),
      midPoint = new Point(0, 1, 0, Units.Meters),
      endPoint = new Point(-1, 0, 0, Units.Meters),
      units = Units.Meters,
      domain = Interval.UnitInterval,
    };

    var bytes = SgeoEncoder.Encode(arc);
    var decoded = (Arc)SgeoDecoder.Decode(bytes);

    decoded.startPoint.x.Should().Be(1);
    decoded.midPoint.y.Should().Be(1);
    decoded.endPoint.x.Should().Be(-1);
    decoded.plane.normal.z.Should().Be(1);
    decoded.radius.Should().Be(1); // derived, recomputed
  }

  [Fact]
  public void Curve_Rational_RoundTrips()
  {
    var curve = new Curve
    {
      degree = 3,
      periodic = false,
      rational = true,
      points = [0, 0, 0, 1, 0, 0, 2, 1, 0, 3, 1, 0],
      weights = [1, 0.5, 0.5, 1],
      knots = [0, 0, 0, 0, 1, 1, 1, 1],
      closed = false,
      units = Units.Meters,
      displayValue = new Polyline { value = [], units = Units.Meters },
    };

    var bytes = SgeoEncoder.Encode(curve);
    SgeoDecoder.ReadHeader(bytes).Flags.Should().HaveFlag(SgeoFlags.Rational);

    var decoded = (Curve)SgeoDecoder.Decode(bytes);
    decoded.degree.Should().Be(3);
    decoded.points.Should().Equal(curve.points);
    decoded.weights.Should().Equal(curve.weights);
    decoded.knots.Should().Equal(curve.knots);
  }

  [Fact]
  public void Curve_NonRational_FillsUnitWeights()
  {
    var curve = new Curve
    {
      degree = 1,
      periodic = false,
      rational = false,
      points = [0, 0, 0, 1, 0, 0],
      weights = [1, 1],
      knots = [0, 0, 1, 1],
      closed = false,
      units = Units.Meters,
      displayValue = new Polyline { value = [], units = Units.Meters },
    };

    var bytes = SgeoEncoder.Encode(curve);
    var decoded = (Curve)SgeoDecoder.Decode(bytes);

    decoded.rational.Should().BeFalse();
    decoded.weights.Should().Equal(1.0, 1.0);
    decoded.points.Should().Equal(curve.points);
  }

  [Fact]
  public void Polycurve_Nested_RoundTrips()
  {
    var polycurve = new Polycurve
    {
      segments =
      [
        new Line
        {
          start = new Point(0, 0, 0, Units.Meters),
          end = new Point(1, 0, 0, Units.Meters),
          units = Units.Meters,
        },
        new Arc
        {
          plane = XYPlane(Units.Meters),
          startPoint = new Point(1, 0, 0, Units.Meters),
          midPoint = new Point(0, 1, 0, Units.Meters),
          endPoint = new Point(-1, 0, 0, Units.Meters),
          units = Units.Meters,
          domain = Interval.UnitInterval,
        },
      ],
      closed = false,
      units = Units.Meters,
    };

    var bytes = SgeoEncoder.Encode(polycurve);
    var decoded = (Polycurve)SgeoDecoder.Decode(bytes);

    decoded.segments.Should().HaveCount(2);
    decoded.segments[0].Should().BeOfType<Line>();
    decoded.segments[1].Should().BeOfType<Arc>();
    ((Line)decoded.segments[0]).end.x.Should().Be(1);
  }

  [Fact]
  public void Pointcloud_WithColorsAndSizes_RoundTrips()
  {
    var pc = new Pointcloud
    {
      points = [0, 0, 0, 1, 1, 1, 2, 2, 2],
      colors = [unchecked((int)0xFFFF0000), unchecked((int)0xFF00FF00), unchecked((int)0xFF0000FF)],
      sizes = [0.1, 0.2, 0.3],
      units = Units.Meters,
    };

    var bytes = SgeoEncoder.Encode(pc);
    var header = SgeoDecoder.ReadHeader(bytes);
    header.Flags.Should().HaveFlag(SgeoFlags.HasColors);
    header.Flags.Should().HaveFlag(SgeoFlags.HasSizes);

    var decoded = (Pointcloud)SgeoDecoder.Decode(bytes);
    decoded.points.Should().Equal(pc.points);
    decoded.colors.Should().Equal(pc.colors);
    decoded.sizes.Should().Equal(pc.sizes);
  }

  [Fact]
  public void Ellipse_WithTrimDomain_RoundTrips()
  {
    var ellipse = new Ellipse
    {
      firstRadius = 5,
      secondRadius = 3,
      plane = XYPlane(Units.Meters),
      domain = Interval.UnitInterval,
      trimDomain = new Interval { start = 0.2, end = 0.8 },
      units = Units.Meters,
    };

    var bytes = SgeoEncoder.Encode(ellipse);
    SgeoDecoder.ReadHeader(bytes).Flags.Should().HaveFlag(SgeoFlags.HasTrimDomain);

    var decoded = (Ellipse)SgeoDecoder.Decode(bytes);
    decoded.firstRadius.Should().Be(5);
    decoded.secondRadius.Should().Be(3);
    decoded.trimDomain!.start.Should().Be(0.2);
    decoded.trimDomain.end.Should().Be(0.8);
  }

  [Fact]
  public void Ellipse_WithoutTrimDomain_RoundTrips()
  {
    var ellipse = new Ellipse
    {
      firstRadius = 5,
      secondRadius = 3,
      plane = XYPlane(Units.Meters),
      domain = Interval.UnitInterval,
      units = Units.Meters,
    };

    var bytes = SgeoEncoder.Encode(ellipse);
    SgeoDecoder.ReadHeader(bytes).Flags.Should().NotHaveFlag(SgeoFlags.HasTrimDomain);

    var decoded = (Ellipse)SgeoDecoder.Decode(bytes);
    decoded.trimDomain.Should().BeNull();
  }

  [Fact]
  public void Spiral_RoundTrips()
  {
    var spiral = new Spiral
    {
      startPoint = new Point(0, 0, 0, Units.Meters),
      endPoint = new Point(10, 0, 5, Units.Meters),
      plane = XYPlane(Units.Meters),
      turns = 2.5,
      pitchAxis = new Vector(0, 0, 1, Units.Meters),
      pitch = 1.5,
      spiralType = SpiralType.Clothoid,
      units = Units.Meters,
      length = 12.3,
      domain = Interval.UnitInterval,
      displayValue = new Polyline { value = [], units = Units.Meters },
    };

    var bytes = SgeoEncoder.Encode(spiral);
    var decoded = (Spiral)SgeoDecoder.Decode(bytes);

    decoded.endPoint.x.Should().Be(10);
    decoded.turns.Should().Be(2.5);
    decoded.pitch.Should().Be(1.5);
    decoded.pitchAxis.z.Should().Be(1);
    decoded.spiralType.Should().Be(SpiralType.Clothoid);
  }

  [Fact]
  public void Box_RoundTrips()
  {
    var box = new Box
    {
      plane = XYPlane(Units.Meters),
      xSize = new Interval { start = 0, end = 1 },
      ySize = new Interval { start = 0, end = 2 },
      zSize = new Interval { start = 0, end = 3 },
      units = Units.Meters,
    };

    var bytes = SgeoEncoder.Encode(box);
    var decoded = (Box)SgeoDecoder.Decode(bytes);

    decoded.xSize.end.Should().Be(1);
    decoded.ySize.end.Should().Be(2);
    decoded.zSize.end.Should().Be(3);
    decoded.volume.Should().Be(6); // derived, recomputed
  }

  // ── integrity ─────────────────────────────────────────────────────────────

  [Fact]
  public void Decode_RejectsCorruptedBody()
  {
    var line = new Line
    {
      start = new Point(0, 0, 0, Units.Meters),
      end = new Point(3, 3, 6, Units.Meters),
      units = Units.Meters,
    };
    var bytes = SgeoEncoder.Encode(line);
    bytes[20] ^= 0xFF; // flip a byte in the body → CRC must fail

    Action act = () => SgeoDecoder.Decode(bytes);
    act.Should().Throw<Speckle.Sdk.SpeckleException>().WithMessage("*CRC*");
  }

  [Fact]
  public void ReadHeader_RejectsBadMagic()
  {
    var bytes = new byte[SgeoFormat.HeaderSize];
    Action act = () => SgeoDecoder.ReadHeader(bytes);
    act.Should().Throw<Speckle.Sdk.SpeckleException>().WithMessage("*magic*");
  }
}
