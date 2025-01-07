using FluentAssertions;
using Speckle.Objects.Geometry;
using Speckle.Objects.Geometry.Autocad;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Xunit;
using Point = Speckle.Objects.Geometry.Point;

namespace Speckle.Objects.Tests.Unit;

public class ObjectBaseValidityTests
{
  [Fact]
  public void TestThatTypeWithoutAttributeFails()
  {
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(Base).Assembly, typeof(Point).Assembly);
  }

  [Fact]
  public void InheritanceTest_Disallow()
  {
    var exception = Assert.Throws<InvalidOperationException>(() =>
    {
      TypeLoader.Reset();
      TypeLoader.Initialize(typeof(Base).Assembly, typeof(Point).Assembly, typeof(Test).Assembly);
    });
    exception.Should().NotBeNull();
    exception
      .Message.Should()
      .Be("Speckle.Objects.Tests.Unit.ObjectBaseValidityTests+Test inherits from Base has no SpeckleTypeAttribute");
  }

  [Fact]
  public void InheritanceTest_Allow()
  {
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(Base).Assembly, typeof(Point).Assembly);
    var fullTypeString = TypeLoader.GetFullTypeString(typeof(AutocadPolycurve));
    fullTypeString.Should().Be("Objects.Geometry.Polycurve:Objects.Geometry.Autocad.AutocadPolycurve");
  }

  public class Test : Polycurve;
}
