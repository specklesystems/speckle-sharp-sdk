using Microsoft.Extensions.Logging.Abstractions;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation.V2.Send;

namespace Speckle.Sdk.Serialization.Tests;

public class ExplicitInterfaceTests
{
  public ExplicitInterfaceTests()
  {
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(Base).Assembly, typeof(TestClass).Assembly);
  }

  [Fact]
  public async Task Test_Json()
  {
    var testClass = new TestClass() { RegularProperty = "Hello" };

    var objects = new Dictionary<string, string>();
    using var process2 = new SerializeProcess(
      null,
      new DummySendCacheManager(objects),
      new DummyServerObjectManager(),
      new BaseChildFinder(new BasePropertyGatherer()),
      new ObjectSerializerFactory(new BasePropertyGatherer()),
      new NullLoggerFactory(),
      new SerializeProcessOptions(false, false, true, true)
    );

    await process2.Serialize(testClass, default);

    await VerifyJsonDictionary(objects);
  }

  [Fact]
  public async Task Test_ExtractAllProperties()
  {
    var testClass = new TestClass() { RegularProperty = "Hello" };

    var gatherer = new BasePropertyGatherer();
    var properties = gatherer.ExtractAllProperties(testClass).ToList();
    await Verify(properties);
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
