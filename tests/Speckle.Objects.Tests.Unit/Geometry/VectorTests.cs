using Speckle.Objects.Geometry;
using Xunit;

namespace Speckle.Objects.Tests.Unit.Geometry;

public class VectorTests
{
  [Theory]
  [InlineData(1d, 2d, 3d, "m")]
  [InlineData(100d, 0d, -200d, "ft")]
  public void TestConstruction(double x, double y, double z, string units)
  {
    var pctor = new Vector(x, y, z, units);

    var init = new Vector
    {
      x = x,
      y = y,
      z = z,
      units = units,
    };

    Assert.Equal(pctor.x, init.x);
    Assert.Equal(pctor.y, init.y);
    Assert.Equal(pctor.z, init.z);
    Assert.Equal(pctor.units, init.units);
  }

  [Theory]
  [InlineData(1d, 0d, 0d, 1d)]
  [InlineData(0d, 2d, 0d, 2d)]
  [InlineData(0d, 0d, -3d, 3d)]
  [InlineData(1d, 1d, 0d, 1.4142135623730951d)]
  public void TestLength(double x, double y, double z, double expected)
  {
    var testCase = new Vector(x, y, z, "");
    var actual = testCase.Length;
    Assert.Equal(actual, expected);
  }
}
