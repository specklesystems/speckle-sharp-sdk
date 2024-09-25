using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;
using Microsoft.Extensions.Logging.Abstractions;
using Speckle.Objects.Geometry;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation;

namespace Speckle.Sdk.Tests.Performance.Benchmarks;

/// <summary>
/// How many threads on our Deserializer is optimal
/// </summary>
[Config(typeof(Config))]
[RankColumn]
[MemoryDiagnoser]
public class GeneralDeserializer : IDisposable
{
  private TestDataHelper _dataSource;

  private class Config : ManualConfig
  {
    public Config()
    {
      var job = Job.ShortRun.WithLaunchCount(0).WithWarmupCount(0).WithIterationCount(1);
      AddJob(job);
    }
  }

  [GlobalSetup]
  public async Task Setup()
  {
    TypeLoader.Initialize(typeof(Base).Assembly, typeof(Point).Assembly);
    var url = "https://latest.speckle.systems/projects/a3ac1b2706/models/59d3b0f3c6"; //small?

    //var url = "https://latest.speckle.systems/projects/2099ac4b5f/models/da511c4d1e"; //perf?
    _dataSource = new TestDataHelper();
    await _dataSource
      .SeedTransport(
        new Account() { serverInfo = new() { url = url } },
        "2099ac4b5f",
        "30fb4cbe6eb2202b9e7b4a4fcc3dd2b6"
      )
      .ConfigureAwait(false);
  }

  [Benchmark]
  public async Task<Base> RunTest()
  {
    SpeckleObjectDeserializer sut = new() { ReadTransport = _dataSource.Transport };
    string data = await _dataSource.Transport.GetObject(_dataSource.ObjectId)!;
    return await sut.DeserializeAsync(data);
  }

  [Benchmark]
  public async Task<Base> RunTest2()
  {
    SpeckleObjectDeserializer2 sut = new() { ReadTransport = _dataSource.Transport };
    string data = await _dataSource.Transport.GetObject(_dataSource.ObjectId)!;
    return await sut.DeserializeAsync(data);
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
