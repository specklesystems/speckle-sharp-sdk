using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Toolchains.CsProj;
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
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class GeneralDeserializer : IDisposable
{ 
  private class Config : ManualConfig
  {
    public Config()
    {
      var job = Job
        .ShortRun.WithLaunchCount(0)
        .WithWarmupCount(0)
        .WithIterationCount(1);
      AddJob(job);
    }
  }
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

    StreamWrapper sw = new("https://latest.speckle.systems/projects/2099ac4b5f/models/da511c4d1e");
    var acc = await sw.GetAccount().ConfigureAwait(false);
    
    using var stage = new ReceiveStage(new Uri(acc.serverInfo.url), sw.StreamId, null);
    return await stage.GetObject(_dataSource.ObjectId, args => { }, default).ConfigureAwait(false);
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
