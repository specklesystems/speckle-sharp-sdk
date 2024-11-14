using NUnit.Framework;
using Shouldly;
using Speckle.Objects.Geometry;
using Speckle.Sdk.Common;
using Xunit;

namespace Speckle.Objects.Tests.Unit.Geometry;

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

  [Fact]
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

    box.area.ShouldBe(2 * (2 * 4 + 2 * 6 + 4 * 6), 0.0001);
    box.volume.ShouldBe(2 * 4 * 6, 0.0001);
  }
}
