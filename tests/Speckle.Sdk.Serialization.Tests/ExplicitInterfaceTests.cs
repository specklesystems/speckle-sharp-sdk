using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation;
using Speckle.Sdk.Serialisation.V2;
using Speckle.Sdk.Serialisation.V2.Send;

namespace Speckle.Sdk.Serialization.Tests;

public class ExplicitInterfaceTests
{
  private readonly ISerializeProcessFactory _factory;

  public ExplicitInterfaceTests()
  {
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(Base).Assembly, typeof(TestClass).Assembly);

    var serviceCollection = new ServiceCollection();
    serviceCollection.AddSpeckleSdk(HostApplications.Navisworks, HostAppVersion.v2023, "Test");
    var serviceProvider = serviceCollection.BuildServiceProvider();

    _factory = serviceProvider.GetRequiredService<ISerializeProcessFactory>();
  }

  [Fact]
  public async Task Test_Json()
  {
    var testClass = new TestClass() { RegularProperty = "Hello" };

    var objects = new ConcurrentDictionary<string, string>();
    await using var serializeProcess = _factory.CreateSerializeProcess(
      new ConcurrentDictionary<Id, Json>(),
      objects,
      null,
      default,
      new SerializeProcessOptions(true, true, false, true)
    );

    await serializeProcess.Serialize(testClass);

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
