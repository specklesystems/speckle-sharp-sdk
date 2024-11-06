using System.Diagnostics.CodeAnalysis;
using NUnit.Framework;
using Shouldly;
using Speckle.Objects.Geometry;
using Speckle.Sdk.Common;
using Xunit;

namespace Speckle.Objects.Tests.Unit.Geometry;

public class PointTests
{
  [Fact]
  [SuppressMessage(
    "Assertion",
    "NUnit2010:Use EqualConstraint for better assertion messages in case of failure",
    Justification = "Need to explicitly test equality operator"
  )]
  public void TestNull()
  {
    Point? a = null;
    Point? b = null;
    Point c = new(0, 0, 0, Units.Meters);

    Assert.Multiple(() =>
    {
      (a == b).ShouldBeTrue();
      (a != b).ShouldBeFalse();
      (b == a).ShouldBeTrue();
      (b != a).ShouldBeFalse();
      
      (a == c).ShouldBeTrue();
      (a != c).ShouldBeFalse();
      (c == a).ShouldBeTrue();
      (c != a).ShouldBeFalse();
    });
  }

  [Theory]
  [InlineData(1, 1, 1, "m", 1, 1, 1, "m", true)]
  [InlineData(1, 1, 1, "m", 0, 1, 1, "m", false)]
  [InlineData(1, 1, 1, "m", 1, 0, 1, "m", false)]
  [InlineData(1, 1, 1, "m", 1, 1, 0, "m", false)]
  [InlineData(1, 1, 1, "", 1, 1, 1, "", true)]
  [InlineData(1, 1, 1, "m", 1, 1, 1, "meters", false)]
  [InlineData(1, 1, 1, "m", 1, 1, 1, "M", false)]
  // Units
  public void TestEqual(double x1, double y1, double z1, string units1, double x2, double y2, double z2, string units2, bool result)
  {
    Point p1 = new(x1, y1, z1, units1);
    Point p2 = new(x2, y2, z2, units2);

    (p1 == p2).ShouldBe(result);
  }
}
