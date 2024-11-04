using NUnit.Framework;
using Speckle.Objects.Geometry;
using Speckle.Sdk.Common;

namespace Speckle.Objects.Tests.Unit.Geometry;

[TestFixture, TestOf(typeof(Box))]
public class BoxTests
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
  public void CanCreateBox()
  {
    const string UNITS = Units.Meters;
    var box = new Box()
    {
      plane = TestPlane,
      xSize = new() { start = -1, end = 1 },
      ySize = new() { start = -2, end = 2 },
      zSize = new() { start = -3, end = 3 },
      units = UNITS,
    };

    Assert.That(box.area, Is.EqualTo(2 * (2 * 4 + 2 * 6 + 4 * 6)).Within(0.0001));
    Assert.That(box.volume, Is.EqualTo(2 * 4 * 6).Within(0.0001));
  }
}
