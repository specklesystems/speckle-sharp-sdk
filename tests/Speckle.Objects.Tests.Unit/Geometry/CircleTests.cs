using NUnit.Framework;
using Speckle.Objects.Geometry;
using Speckle.Sdk.Common;

namespace Speckle.Objects.Tests.Unit.Geometry;

[TestFixture, TestOf(typeof(Circle))]
public class CircleTests
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
  public void CanCreateCircle()
  {
    const string UNITS = Units.Meters;
    var circle = new Circle()
    {
      plane = TestPlane,
      radius = 5,
      units = UNITS,
    };

    Assert.That(circle.length, Is.EqualTo(2 * Math.PI * 5).Within(0.0001));
    Assert.That(circle.area, Is.EqualTo(Math.PI * 5 * 5).Within(0.0001));
  }
}
