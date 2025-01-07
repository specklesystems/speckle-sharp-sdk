using FluentAssertions;

using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Tests.Unit.Models.TestModels;
using Xunit;

namespace Speckle.Sdk.Tests.Unit.Models
{
  public class SpeckleTypeTests
  {
    public SpeckleTypeTests()
    {
      // Setup logic during test class initialization
      TypeLoader.Reset();
      TypeLoader.Initialize(typeof(Base).Assembly, typeof(Foo).Assembly);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void SpeckleTypeIsProperlyBuilt(Base foo, string expectedType)
    {
      foo.speckle_type.Should().Be(expectedType);
    }

    public static IEnumerable<object[]> Cases =>
      new List<object[]>
      {
        new object[] { new Base(), "Base" },
        new object[] { new Foo(), "TestModels.Foo" },
        new object[] { new Bar(), "TestModels.Foo:TestModels.Bar" },
        new object[] { new Baz(), "TestModels.Foo:TestModels.Bar:TestModels.Baz" },
      };
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
}
