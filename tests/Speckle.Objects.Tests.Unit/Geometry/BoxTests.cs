using Speckle.Objects.Geometry;
using Speckle.Sdk.Common;
using Shouldly;
using Xunit;

namespace Speckle.Objects.Tests.Unit.Geometry;

public class BoxTests
{
  private Plane TestPlane =>
    new()
    {
      origin = new Point(0, 0, 0, Units.Meters),
      normal = new Vector(0, 0, 1, Units.Meters),
      xdir = new Vector(1, 0, 0, Units.Meters),
      ydir = new Vector(0, 1, 0, Units.Meters),
      units = Units.Meters,
    };

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

    // Assert area
    box.area.ShouldBe(2 * (2 * 4 + 2 * 6 + 4 * 6), 0.0001);

    // Assert volume
    box.volume.ShouldBe(2 * 4 * 6, 0.0001);
  }
}
