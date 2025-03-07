using FluentAssertions;
using Speckle.Objects.Geometry;
using Speckle.Sdk.Common;

namespace Speckle.Objects.Tests.Unit.Geometry;

public class CircleTests
{
  private Plane TestPlane
  {
    get
    {
      const string UNITS = Units.Meters;
      return new Plane
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
    var circle = new Circle
    {
      plane = TestPlane,
      radius = 5,
      units = UNITS,
    };

    // Use Shouldly assertions
    circle.length.Should().BeApproximately(2 * Math.PI * 5, 0.0001);
    circle.area.Should().BeApproximately(Math.PI * 5 * 5, 0.0001);
  }
}
