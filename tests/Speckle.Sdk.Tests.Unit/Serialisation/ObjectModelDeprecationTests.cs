using AwesomeAssertions;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation.Deprecated;

namespace Speckle.Sdk.Tests.Unit.Serialisation
{
  public class TypeLoaderTests
  {
    // Constructor replaces the [SetUp] functionality in NUnit
    public TypeLoaderTests()
    {
      TypeLoader.Reset();
      TypeLoader.Initialize(typeof(Base).Assembly, typeof(MySpeckleBase).Assembly);
    }

    [Fact] // Replaces [Test]
    public void TestThatTypeWithoutAttributeFails()
    {
      // Record.Exception is the xUnit alternative of Assert.Throws
      var exception = Record.Exception(() => TypeLoader.ParseType(typeof(string)));

      exception.Should().NotBeNull(); // Shouldly assertion
      exception.Should().BeOfType<InvalidOperationException>(); // Ensure it's the correct exception type
    }

    [Fact] // Replaces [Test]
    public void TestThatTypeWithoutMultipleAttributes()
    {
      string destinationType = $"Speckle.Core.Serialisation.{nameof(MySpeckleBase)}";

      var result = TypeLoader.GetAtomicType(destinationType);
      result.Should().Be<MySpeckleBase>(); // Shouldly assertion replaces Assert.That

      destinationType = $"Speckle.Core.Serialisation.Deprecated.{nameof(MySpeckleBase)}";

      result = TypeLoader.GetAtomicType(destinationType);
      result.Should().Be<MySpeckleBase>(); // Shouldly assertion replaces Assert.That
    }
  }
}

namespace Speckle.Sdk.Serialisation.Deprecated
{
  [SpeckleType("Speckle.Core.Serialisation.MySpeckleBase")]
  [DeprecatedSpeckleType("Speckle.Core.Serialisation.Deprecated.MySpeckleBase")]
  public class MySpeckleBase : Base { }
}
