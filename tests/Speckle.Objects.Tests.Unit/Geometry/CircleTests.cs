using NUnit.Framework;
using Shouldly;
using Speckle.Objects.Geometry;
using Speckle.Sdk.Common;
using Xunit;

namespace Speckle.Objects.Tests.Unit.Geometry;

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

  [Fact]
  public void CanCreateCircle()
  {
    const string UNITS = Units.Meters;
    var circle = new Circle()
    {
      plane = TestPlane,
      radius = 5,
      units = UNITS,
    };

    circle.length.ShouldBe(2 * Math.PI * 5, 0.0001);
    circle.area.ShouldBe(Math.PI * 5 * 5, 0.0001);
  }
}
