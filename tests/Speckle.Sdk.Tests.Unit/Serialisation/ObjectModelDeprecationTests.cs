using AwesomeAssertions;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation.Deprecated;

namespace Speckle.Sdk.Tests.Unit.Serialisation
{
  public class TypeLoaderTests
  {
    [Fact]
    public void TestThatTypeWithoutAttributeFails()
    {
      var exception = Record.Exception(() => TypeLoader.ParseType(typeof(string)));

      exception.Should().NotBeNull();
      exception.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public void TestThatTypeWithoutMultipleAttributes()
    {
      string destinationType = $"Speckle.Core.Serialisation.{nameof(MySpeckleBase)}";

      var result = TypeLoader.GetAtomicType(destinationType);
      result.Should().Be<MySpeckleBase>();

      destinationType = $"Speckle.Core.Serialisation.Deprecated.{nameof(MySpeckleBase)}";

      result = TypeLoader.GetAtomicType(destinationType);
      result.Should().Be<MySpeckleBase>();
    }
  }
}

namespace Speckle.Sdk.Serialisation.Deprecated
{
  [SpeckleType("Speckle.Core.Serialisation.MySpeckleBase")]
  [DeprecatedSpeckleType("Speckle.Core.Serialisation.Deprecated.MySpeckleBase")]
  public class MySpeckleBase : Base { }
}
