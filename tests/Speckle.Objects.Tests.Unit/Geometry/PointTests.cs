using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using Speckle.Objects.Geometry;
using Speckle.Sdk.Common;

namespace Speckle.Objects.Tests.Unit.Geometry;

public class PointTests
{
  [Fact]
  [SuppressMessage(
    "Assertion",
    "xUnit2013:Do not use equality check to assert boolean value",
    Justification = "Explicit equality operator tests are necessary"
  )]
  public void TestNull()
  {
    Point? a = null;
    Point? b = null;
    Point c = new(0, 0, 0, Units.Meters);

    a.Should().Be(b);
    (a != b).Should().BeFalse();

    b.Should().Be(a);
    (b != a).Should().BeFalse();

    (a == c).Should().BeFalse();
    (a != c).Should().BeTrue();

    (c == a).Should().BeFalse();
    (c != a).Should().BeTrue();
  }

  //TODO: Should(). units be allowed to be string?
  [Theory]
  [InlineData(1, 1, 1, "m", 1, 1, 1, "m", true)]
  [InlineData(1, 1, 1, "m", 0, 1, 1, "m", false)]
  [InlineData(1, 1, 1, "m", 1, 0, 1, "m", false)]
  [InlineData(1, 1, 1, "m", 1, 1, 0, "m", false)]
  [InlineData(1, 1, 1, "", 1, 1, 1, "", false)]
  [InlineData(1, 1, 1, null, 1, 1, 1, null, false)]
  [InlineData(1, 1, 1, "m", 1, 1, 1, "meters", false)]
  [InlineData(1, 1, 1, "m", 1, 1, 1, "M", false)]
  public void TestEqual(
    double x1,
    double y1,
    double z1,
    string? units1,
    double x2,
    double y2,
    double z2,
    string? units2,
    bool expectedResult
  )
  {
    if (string.IsNullOrEmpty(units1) || string.IsNullOrEmpty(units2))
    {
      expectedResult.Should().BeFalse();
      return;
    }
    Point p1 = new(x1, y1, z1, units1);
    Point p2 = new(x2, y2, z2, units2);

    (p1 == p2).Should().Be(expectedResult);
  }
}
