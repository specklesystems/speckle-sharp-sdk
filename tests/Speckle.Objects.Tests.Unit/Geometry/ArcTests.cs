using FluentAssertions;
using Speckle.Objects.Geometry;
using Speckle.Sdk.Common;

namespace Speckle.Objects.Tests.Unit.Geometry;

public class ArcTests
{
  private Plane TestPlaneCounterClockwise =>
    new()
    {
      origin = new Point(0, 0, 0, Units.Meters),
      normal = new Vector(0, 0, 1, Units.Meters),
      xdir = new Vector(1, 0, 0, Units.Meters),
      ydir = new Vector(0, 1, 0, Units.Meters),
      units = Units.Meters,
    };

  private Plane TestPlaneClockwise =>
    new()
    {
      origin = new Point(0, 0, 0, Units.Meters),
      normal = new Vector(0, 0, -1, Units.Meters),
      xdir = new Vector(-1, 0, 0, Units.Meters),
      ydir = new Vector(0, 1, 0, Units.Meters),
      units = Units.Meters,
    };

  [Fact]
  public void CanCreateArc_HalfCircle_CounterClockwise()
  {
    var counterClockwiseArc = new Arc()
    {
      plane = TestPlaneCounterClockwise,
      startPoint = new Point(1, 0, 0, Units.Meters),
      endPoint = new Point(-1, 0, 0, Units.Meters),
      midPoint = new Point(0, 1, 0, Units.Meters),
      units = Units.Meters,
    };

    Point.Distance(counterClockwiseArc.midPoint, new Point(0, 1, 0, Units.Meters)).Should().BeApproximately(0, 0.0001);

    Point
      .Distance(counterClockwiseArc.plane.origin, new Point(0, 0, 0, Units.Meters))
      .Should()
      .BeApproximately(0, 0.0001);

    (counterClockwiseArc.measure - Math.PI).Should().BeApproximately(0, 0.0001);

    counterClockwiseArc.radius.Should().BeApproximately(1, 0.0001);

    counterClockwiseArc.length.Should().BeApproximately(Math.PI, 0.0001);
  }

  [Fact]
  public void CanCreateArc_HalfCircle_Clockwise()
  {
    var clockwiseArc = new Arc()
    {
      plane = TestPlaneClockwise,
      endPoint = new Point(1, 0, 0, Units.Meters),
      startPoint = new Point(-1, 0, 0, Units.Meters),
      midPoint = new Point(0, 1, 0, Units.Meters),
      units = Units.Meters,
    };

    Point.Distance(clockwiseArc.midPoint, new Point(0, 1, 0, Units.Meters)).Should().BeApproximately(0, 0.0001);

    Point.Distance(clockwiseArc.plane.origin, new Point(0, 0, 0, Units.Meters)).Should().BeApproximately(0, 0.0001);

    (clockwiseArc.measure - Math.PI).Should().BeApproximately(0, 0.0001);

    clockwiseArc.radius.Should().BeApproximately(1, 0.0001);

    clockwiseArc.length.Should().BeApproximately(Math.PI, 0.0001);
  }
}
