using NUnit.Framework;
using Shouldly;
using Speckle.Objects.Geometry;
using Speckle.Objects.Geometry.Autocad;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Point = Speckle.Objects.Geometry.Point;

namespace Speckle.Objects.Tests.Unit;

public class ObjectBaseValidityTests
{
  [Test]
  public void TestThatTypeWithoutAttributeFails()
  {
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(Base).Assembly, typeof(Point).Assembly);
  }

  [Test]
  public void InheritanceTest_Disallow()
  {
    var exception = Assert.Throws<InvalidOperationException>(() =>
    {
      TypeLoader.Reset();
      TypeLoader.Initialize(typeof(Base).Assembly, typeof(Point).Assembly, typeof(Test).Assembly);
    });
    exception.ShouldNotBeNull();
    exception.Message.ShouldBe(
      "Speckle.Objects.Tests.Unit.ObjectBaseValidityTests+Test inherits from Base has no SpeckleTypeAttribute"
    );
  }

  [Test]
  public void InheritanceTest_Allow()
  {
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(Base).Assembly, typeof(Point).Assembly);
    var fullTypeString = TypeLoader.GetFullTypeString(typeof(AutocadPolycurve));
    fullTypeString.ShouldBe("Objects.Geometry.Polycurve:Objects.Geometry.Autocad.AutocadPolycurve");
  }

  public class Test : Polycurve;
}
