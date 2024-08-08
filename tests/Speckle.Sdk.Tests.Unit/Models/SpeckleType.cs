using NUnit.Framework;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using TestModels;

namespace Speckle.Sdk.Tests.Unit.Models
{
  [TestFixture]
  [TestOf(typeof(Base))]
  public class SpeckleTypeTests
  {
    [SetUp]
    public void Setup()
    {
      TypeLoader.Reset();
      TypeLoader.Initialize(typeof(Base).Assembly, typeof(Foo).Assembly);
    }

    [Test, TestCaseSource(nameof(s_cases))]
    public void SpeckleTypeIsProperlyBuilt(Base foo, string expectedType)
    {
      Assert.That(foo.speckle_type, Is.EqualTo(expectedType));
    }

    private static readonly object[] s_cases =
    {
      new object[] { new Base(), "Base" },
      new object[] { new Foo(), "TestModels.Foo" },
      new object[] { new Bar(), "TestModels.Foo:TestModels.Bar" },
      new object[] { new Baz(), "TestModels.Foo:TestModels.Bar:TestModels.Baz" }
    };
  }
}

namespace TestModels
{
  [SpeckleType("TestModels.Foo")]
  public class Foo : Base { }

  [SpeckleType("TestModels.Bar")]
  public class Bar : Foo { }

  [SpeckleType("TestModels.Baz")]
  public class Baz : Bar { }
}
