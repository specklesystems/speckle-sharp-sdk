using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Toolchains.CsProj;
using Speckle.Objects.Geometry;
using Speckle.Sdk.Common;
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
      var baseJob = Job.ShortRun.WithLaunchCount(1).WithWarmupCount(0).WithIterationCount(1)
        .WithToolchain(CsProjCoreToolchain.NetCoreApp80);

      var newJob = baseJob.WithSpeckle("3.1.0-dev.121");
      var oldJob = baseJob.WithSpeckle("3.1.0-dev.124");
      
      //AddJob(baseJob.WithNuGet("Speckle.Sdk", "3.1.0-dev.122").WithId("3.1.0-dev.122"));
      AddJob(newJob);
      AddJob(oldJob);
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
    SpeckleObjectDeserializer sut = new() { ReadTransport = _dataSource.Transport.NotNull() };
    return await sut.Deserialize(_dataSource.Transport.NotNull().GetObject(_dataSource.ObjectId.NotNull()).NotNull());
  }
  [GlobalCleanup]
  public void Cleanup() => Dispose();

  public void Dispose() => _dataSource.Dispose();
}
