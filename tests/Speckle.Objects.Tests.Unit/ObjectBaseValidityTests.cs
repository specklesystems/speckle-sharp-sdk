using NUnit.Framework;
using Shouldly;
using Speckle.Objects.Geometry;
using Speckle.Objects.Structural.GSA.Geometry;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;

namespace Objects.Tests.Unit;

public class ObjectBaseValidityTests
{
  [Test]
  public void TestThatTypeWithoutAttributeFails()
  {
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(Base).Assembly, typeof(GSAAssembly).Assembly);
  }
  
  [Test]
  public void InheritanceTest()
  {
    var exception = Assert.Throws<InvalidOperationException>(() =>
    {
      TypeLoader.Reset();
      TypeLoader.Initialize(typeof(Base).Assembly, typeof(GSAAssembly).Assembly, typeof(Test).Assembly);
    });
    exception.ShouldNotBeNull();
    exception.Message.ShouldBe("Objects.Tests.Unit.ObjectBaseValidityTests+Test inherits from Base has no SpeckleTypeAttribute");
  }

  public class Test : Polycurve;
}
