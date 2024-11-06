using NUnit.Framework;
using Shouldly;
using Speckle.Objects.Geometry;
using Speckle.Sdk.Common;
using Xunit;

namespace Speckle.Objects.Tests.Unit.Geometry;

public class ArcTests
{
  private Plane TestPlaneCounterClockwise
  {
    get
    {
      const string UNITS = Units.Meters;
      return new()
      {
        origin = new Point(0, 0, 0, UNITS),
        normal = new Vector(0, 0, 1, UNITS),
        xdir = new Vector(1, 0, 0, UNITS),
        ydir = new Vector(0, 1, 0, UNITS),
        units = UNITS,
      };
    }
  }

  private Plane TestPlaneClockwise
  {
    get
    {
      const string UNITS = Units.Meters;
      return new()
      {
        origin = new Point(0, 0, 0, UNITS),
        normal = new Vector(0, 0, -1, UNITS),
        xdir = new Vector(-1, 0, 0, UNITS),
        ydir = new Vector(0, 1, 0, UNITS),
        units = UNITS,
      };
    }
  }

  [Fact]
  public void CanCreateArc_HalfCircle_CounterClockwise()
  {
    const string UNITS = Units.Meters;
    var counterClockwiseArc = new Arc()
    {
      plane = TestPlaneCounterClockwise,
      startPoint = new Point(1, 0, 0, UNITS),
      endPoint = new Point(-1, 0, 0, UNITS),
      midPoint = new Point(0, 1, 0, UNITS),
      units = UNITS,
    };

    Point.Distance(counterClockwiseArc.midPoint, new Point(0, 1, 0, UNITS)).ShouldBe(0, 0.0001);
   Point.Distance(counterClockwiseArc.plane.origin, new Point(0, 0, 0, UNITS)).ShouldBe(0, 0.0001);
   ( counterClockwiseArc.measure - Math.PI).ShouldBe(0, 0.0001);
  counterClockwiseArc.radius.ShouldBe(1, 0.0001);
  counterClockwiseArc.length.ShouldBe(Math.PI, 0.0001);
  }

  [Fact]
  public void CanCreateArc_HalfCircle_Clockwise()
  {
    const string UNITS = Units.Meters;
    var counterClockwiseArc = new Arc()
    {
      plane = TestPlaneClockwise,
      endPoint = new Point(1, 0, 0, UNITS),
      startPoint = new Point(-1, 0, 0, UNITS),
      midPoint = new Point(0, 1, 0, UNITS),
      units = UNITS,
    };

    Point.Distance(counterClockwiseArc.midPoint, new Point(0, 1, 0, UNITS)).ShouldBe(0, 0.0001);
    Point.Distance(counterClockwiseArc.plane.origin, new Point(0, 0, 0, UNITS)).ShouldBe(0, 0.0001);
    ( counterClockwiseArc.measure - Math.PI).ShouldBe(0, 0.0001);
    counterClockwiseArc.radius.ShouldBe(1, 0.0001);
    counterClockwiseArc.length.ShouldBe(Math.PI, 0.0001);
  }
}
