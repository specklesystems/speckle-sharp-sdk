using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Speckle.Objects.Data;
using Speckle.Objects.Geometry;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation;
using Speckle.Sdk.Serialisation.V2;
using Speckle.Sdk.Serialisation.V2.Send;

namespace Speckle.Sdk.Serialization.Tests;

public class DataObjectTests
{
  private readonly ISerializeProcessFactory _factory;

  public DataObjectTests()
  {
    var serviceCollection = new ServiceCollection();
    serviceCollection.AddSpeckleSdk(new("Tests", "test"), "v3", typeof(TestClass).Assembly, typeof(Polyline).Assembly);
    var serviceProvider = serviceCollection.BuildServiceProvider();

    _factory = serviceProvider.GetRequiredService<ISerializeProcessFactory>();
  }

  [Theory]
  [InlineData(typeof(ArcgisObject))]
  [InlineData(typeof(ArchicadObject))]
  [InlineData(typeof(Civil3dObject))]
  [InlineData(typeof(DataObject))]
  [InlineData(typeof(EtabsObject))]
  [InlineData(typeof(NavisworksObject))]
  [InlineData(typeof(RevitObject))]
  [InlineData(typeof(TeklaObject))]
  public async Task ValidateDataObject(Type type)
  {
    Base x = (Base)(Activator.CreateInstance(type) ?? throw new Exception("Could not create instance of " + type.Name));

    var json = new ConcurrentDictionary<Id, Json>();
    await using var serializeProcess = _factory.CreateSerializeProcess(
      new MemoryJsonCacheManager(json),
      new DummyServerObjectManager(),
      null,
      default,
      new SerializeProcessOptions(true, true, false, true)
    );
    await serializeProcess.Serialize(x);
    await VerifyJson(json.Single().Value.Value).UseParameters(type);
  }
}
