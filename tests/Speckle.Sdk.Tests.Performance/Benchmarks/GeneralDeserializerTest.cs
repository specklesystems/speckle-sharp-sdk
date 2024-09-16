using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Speckle.Objects.Geometry;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation;

namespace Speckle.Sdk.Tests.Performance.Benchmarks;

/// <summary>
/// How many threads on our Deserializer is optimal
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Monitoring)]
public class GeneralDeserializer : IDisposable
{
  private TestDataHelper _dataSource;

  [GlobalSetup]
  public async Task Setup()
  {
    TypeLoader.Initialize(typeof(Base).Assembly, typeof(Point).Assembly);
    _dataSource = new TestDataHelper();
    await _dataSource
      .SeedTransport(new("https://latest.speckle.systems/projects/2099ac4b5f/models/da511c4d1e"))
      .ConfigureAwait(false);
  }

  [Benchmark]
  public async Task<Base> RunTest()
  {
    SpeckleObjectDeserializer sut = new() { ReadTransport = _dataSource.Transport };
    string data = await _dataSource.Transport.GetObject(_dataSource.ObjectId)!;
    return await sut.DeserializeJsonAsync(data);
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
