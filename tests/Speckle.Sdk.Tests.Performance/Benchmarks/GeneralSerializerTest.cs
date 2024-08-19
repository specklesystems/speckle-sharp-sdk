using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Tests.Performance.Benchmarks;

/// <summary>
/// How many threads on our Deserializer is optimal
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Monitoring)]
public class GeneralSerializer : IDisposable
{
  private TestDataHelper _dataSource;
  private List<Base> _bases =new();

  [GlobalSetup]
  public async Task Setup()
  {
    _dataSource = new TestDataHelper();
    await _dataSource.SeedTransport(new Uri("https://latest.speckle.systems/projects/2099ac4b5f/models/97945f6c6f")).ConfigureAwait(false);
    
    BaseObjectDeserializerV2 sut = new() { ReadTransport = _dataSource.Transport };
    var b = sut.Deserialize(_dataSource.Transport.GetObject(_dataSource.ObjectId)!);
    _bases.Add(b);
    
  }

  [Benchmark]
  public void RunTest()
  {
    BaseObjectSerializerV2 sut = new(new List<ITransport>() { new MemoryTransport() });
    sut.Serialize(_bases.First());
  }

  [GlobalCleanup]
  public void Cleanup()
  {
    Dispose();
  }

  public void Dispose()
  {
    _dataSource.Dispose();
  }
}
