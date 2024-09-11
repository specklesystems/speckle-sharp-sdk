using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Toolchains.CsProj;
using Speckle.Objects.Geometry;
using Speckle.Sdk.Api;
using Speckle.Sdk.Common;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation;
using Speckle.Sdk.Serialisation.Receive;

namespace Speckle.Sdk.Tests.Performance.Benchmarks;

/// <summary>
/// How many threads on our Deserializer is optimal
/// </summary>
[Config(typeof(Config))]
[RankColumn]
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class GeneralDeserializer
{
  private class Config : ManualConfig
  {
    public Config()
    {
      var job = Job.ShortRun.WithLaunchCount(0).WithWarmupCount(0).WithIterationCount(1);
      AddJob(job);
    }
  }

  [GlobalSetup]
  public void Setup()
  {
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(Base).Assembly, typeof(Point).Assembly);
    /* _dataSource = new TestDataHelper();
     await _dataSource
       .SeedTransport(new("https://latest.speckle.systems/projects/2099ac4b5f/models/da511c4d1e"))
       .ConfigureAwait(false);*/
  }

  [Benchmark]
  public Task<Base> TwoDownloadFourDeserializer()
  {
    return RunTest(new ReceiveProcessSettings() { MaxDownloadThreads = 2, MaxDeserializeThreads = 4 });
  }

  [Benchmark]
  public Task<Base> FourDownloadFourDeserializer()
  {
    return RunTest(new ReceiveProcessSettings() { MaxDownloadThreads = 4, MaxDeserializeThreads = 4 });
  }

  [Benchmark]
  public Task<Base> FourDownload8Deserializer()
  {
    return RunTest(new ReceiveProcessSettings() { MaxDownloadThreads = 4, MaxDeserializeThreads = 8 });
  }

  [Benchmark]
  public Task<Base> TwoDownloadFourDeserializerHalfMaxSize()
  {
    return RunTest(
      new ReceiveProcessSettings()
      {
        MaxDownloadThreads = 2,
        MaxDeserializeThreads = 4,
        MaxObjectRequestSize = 5000
      }
    );
  }

  private async Task<Base> RunTest(ReceiveProcessSettings receiveProcessSettings)
  {
    /*SpeckleObjectDeserializer sut = new() { ReadTransport = _dataSource.Transport };
    string data = await _dataSource.Transport.GetObject(_dataSource.ObjectId)!;
    return await sut.DeserializeJsonAsync(data);*/
    var url = "https://latest.speckle.systems/projects/a3ac1b2706/models/59d3b0f3c6"; //small?

    //var url = "https://latest.speckle.systems/projects/2099ac4b5f/models/da511c4d1e"; //perf?

    StreamWrapper sw = new(url);
    var acc = await sw.GetAccount().ConfigureAwait(false);
    var rootObject = await Operations.Receive2(acc, sw.StreamId, sw.BranchName!, args => {
    });

    return rootObject;
  }
}
