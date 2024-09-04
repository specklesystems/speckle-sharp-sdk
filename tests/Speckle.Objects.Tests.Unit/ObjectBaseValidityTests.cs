using NUnit.Framework;
using Shouldly;
using Speckle.Objects.Geometry;
using Speckle.Objects.Geometry.Autocad;
using Speckle.Objects.Structural.GSA.Geometry;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Tests.Unit;

public class ObjectBaseValidityTests
{
  [Test]
  public void TestThatTypeWithoutAttributeFails()
  {
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(Base).Assembly, typeof(GSAAssembly).Assembly);
  }

  [Test]
  public void InheritanceTest_Disallow()
  {
    var exception = Assert.Throws<InvalidOperationException>(() =>
    {
      TypeLoader.Reset();
      TypeLoader.Initialize(typeof(Base).Assembly, typeof(GSAAssembly).Assembly, typeof(Test).Assembly);
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
    TypeLoader.Initialize(typeof(Base).Assembly, typeof(GSAAssembly).Assembly);
    var fullTypeString = TypeLoader.GetFullTypeString(typeof(AutocadPolycurve));
    fullTypeString.ShouldBe("Speckle.Objects.Geometry.Polycurve:Speckle.Objects.Geometry.Autocad.AutocadPolycurve");
  }

  public class Test : Polycurve;
}
