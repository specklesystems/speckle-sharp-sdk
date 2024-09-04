using NUnit.Framework;
using Speckle.Objects.Geometry;
using Speckle.Sdk.Common;

namespace Speckle.Objects.Tests.Unit.Geometry;

[TestFixture, TestOf(typeof(Arc))]
public class ArcTests
{
  private Plane TestPlane
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

  [Test]
  public void CanCreateArc_HalfCircle()
  {
    const string UNITS = Units.Meters;
    var arc = new Arc(TestPlane, new Point(-5, 5, 0, UNITS), new Point(5, 5, 0, UNITS), Math.PI);

    Assert.That(arc.startAngle, Is.EqualTo(0));
    Assert.That(arc.endAngle, Is.EqualTo(Math.PI));

    Assert.That(Point.Distance(arc.midPoint, new Point(0, 0, 0, UNITS)), Is.EqualTo(0).Within(0.0001));
    Assert.That(Point.Distance(arc.plane.origin, new Point(0, 5, 0, UNITS)), Is.EqualTo(0).Within(0.0001));
  }
}
