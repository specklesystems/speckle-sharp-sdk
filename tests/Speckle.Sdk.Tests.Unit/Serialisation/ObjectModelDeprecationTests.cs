using Shouldly;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation.Deprecated;

namespace Speckle.Sdk.Tests.Unit.Serialisation
{
  public class TypeLoaderTests
  {
    [Before(Class)]
    public void Setup()
    {
      TypeLoader.Reset();
      TypeLoader.Initialize(typeof(Base).Assembly, typeof(MySpeckleBase).Assembly);
    }

    [Test]
    public void TestThatTypeWithoutAttributeFails()
    {
      var e = Assert.Throws<InvalidOperationException>(() => TypeLoader.ParseType(typeof(string)));
      e.ShouldNotBeNull();
    }

    [Test]
    public void TestThatTypeWithoutMultipleAttributes()
    {
      string destinationType = $"Speckle.Core.Serialisation.{nameof(MySpeckleBase)}";

      var result = TypeLoader.GetAtomicType(destinationType);
      result.ShouldBe(typeof(MySpeckleBase));

      destinationType = $"Speckle.Core.Serialisation.Deprecated.{nameof(MySpeckleBase)}";

      result = TypeLoader.GetAtomicType(destinationType);
      result.ShouldBe(typeof(MySpeckleBase));
    }
  }
}

namespace Speckle.Sdk.Serialisation.Deprecated
{
  [SpeckleType("Speckle.Core.Serialisation.MySpeckleBase")]
  [DeprecatedSpeckleType("Speckle.Core.Serialisation.Deprecated.MySpeckleBase")]
  public class MySpeckleBase : Base { }
}
