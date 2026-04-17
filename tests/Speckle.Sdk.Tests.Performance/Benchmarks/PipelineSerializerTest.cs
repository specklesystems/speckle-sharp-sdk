using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Microsoft.Extensions.DependencyInjection;
using Speckle.Objects.Geometry;
using Speckle.Sdk.Api;
using Speckle.Sdk.Models.Collections;
using Speckle.Sdk.Pipelines.Send;

namespace Speckle.Sdk.Tests.Performance.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RunStrategy.Monitoring, 0, 0, 4)]
public class PipelineSerializerTest
{
  private readonly Serializer _sut = new();
  private ServiceProvider _provider;
  private Collection testData;

  [GlobalSetup]
  public async Task Setup()
  {
    var serviceCollection = new ServiceCollection();
    serviceCollection.AddSpeckleSdk(new("Tests", "test"), "v3", assemblies: typeof(Mesh).Assembly);
    _provider = serviceCollection.BuildServiceProvider();

    var operations = _provider.GetRequiredService<IOperations>();
    testData = (Collection)
      await operations.Receive2(
        new("https://app.speckle.systems"),
        "bf5b49215c",
        "feff8c11a06597d3a7740738a55417d2",
        null,
        null,
        default
      );
  }

  [Benchmark]
  public List<UploadItem[]> Serialize()
  {
    List<UploadItem[]> items = new(testData.elements.Count);
    foreach (var item in testData.elements)
    {
      items.Add(_sut.Serialize(item).ToArray());
    }

    return items;
  }
}
