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
    var arc = new Arc()
    {
      plane = TestPlane,
      startPoint = new Point(1, 0, 0, UNITS),
      endPoint = new Point(-1, 0, 0, UNITS),
      midPoint = new Point(0, 1, 0, UNITS),
      units = UNITS,
    };

    Assert.That(Point.Distance(arc.midPoint, new Point(0, 1, 0, UNITS)), Is.EqualTo(0).Within(0.0001));
    Assert.That(Point.Distance(arc.plane.origin, new Point(0, 0, 0, UNITS)), Is.EqualTo(0).Within(0.0001));
  }
}
