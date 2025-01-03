using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation.V2.Send;
using Xunit;

namespace Speckle.Sdk.Serialization.Tests;

public class ExplicitInterfaceTests
{
  // Constructor to replace [SetUp]
  public ExplicitInterfaceTests()
  {
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(Base).Assembly, typeof(TestClass).Assembly);
  }

  [Fact] // Replaces [Test]
  public async Task Test_Json()
  {
    var testClass = new TestClass() { RegularProperty = "Hello" };

    var objects = new Dictionary<string, string>();
    var process2 = new SerializeProcess(
      null,
      new DummySendCacheManager(objects),
      new DummyServerObjectManager(),
      new BaseChildFinder(new BasePropertyGatherer()),
      new ObjectSerializerFactory(new BasePropertyGatherer()),
      new SerializeProcessOptions(false, false, true, true)
    );

    await process2.Serialize(testClass, default);

    objects.Count.ShouldBe(1);
    objects["daaa67cfd73a957247cf2d631b7ca4f3"]
      .ShouldBe(
        "{\"RegularProperty\":\"Hello\",\"applicationId\":null,\"speckle_type\":\"Speckle.Core.Serialisation.TestClass\",\"id\":\"daaa67cfd73a957247cf2d631b7ca4f3\"}"
      );
  }

  [Fact] // Replaces [Test]
  public void Test_ExtractAllProperties()
  {
    var testClass = new TestClass() { RegularProperty = "Hello" };

    var gatherer = new BasePropertyGatherer();
    var properties = gatherer.ExtractAllProperties(testClass).ToList();

    properties.Count.ShouldBe(3);
    properties.Select(x => x.Name).ShouldContain("RegularProperty");
    properties.Select(x => x.Name).ShouldNotContain("TestProperty");
  }
}

[SpeckleType("Speckle.Core.Serialisation.TestClass")]
public sealed class TestClass : Base, ITestInterface
{
  public string RegularProperty { get; set; }
  string ITestInterface.TestProperty => RegularProperty;
}

public interface ITestInterface
{
  string TestProperty { get; }
}
