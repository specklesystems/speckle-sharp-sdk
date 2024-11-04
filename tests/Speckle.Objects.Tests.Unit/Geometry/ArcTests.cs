using NUnit.Framework;
using Speckle.Objects.Geometry;
using Speckle.Sdk.Common;

namespace Speckle.Objects.Tests.Unit.Geometry;

[TestFixture, TestOf(typeof(Arc))]
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

  [Test]
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

    Assert.That(Point.Distance(counterClockwiseArc.midPoint, new Point(0, 1, 0, UNITS)), Is.EqualTo(0).Within(0.0001));
    Assert.That(
      Point.Distance(counterClockwiseArc.plane.origin, new Point(0, 0, 0, UNITS)),
      Is.EqualTo(0).Within(0.0001)
    );
    Assert.That(counterClockwiseArc.measure - Math.PI, Is.EqualTo(0).Within(0.0001));
    Assert.That(counterClockwiseArc.radius, Is.EqualTo(1).Within(0.0001));
    Assert.That(counterClockwiseArc.length, Is.EqualTo(Math.PI).Within(0.0001));
  }

  [Test]
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

    Assert.That(Point.Distance(counterClockwiseArc.midPoint, new Point(0, 1, 0, UNITS)), Is.EqualTo(0).Within(0.0001));
    Assert.That(
      Point.Distance(counterClockwiseArc.plane.origin, new Point(0, 0, 0, UNITS)),
      Is.EqualTo(0).Within(0.0001)
    );
    Assert.That(counterClockwiseArc.measure - Math.PI, Is.EqualTo(0).Within(0.0001));
    Assert.That(counterClockwiseArc.radius, Is.EqualTo(1).Within(0.0001));
    Assert.That(counterClockwiseArc.length, Is.EqualTo(Math.PI).Within(0.0001));
  }
}
