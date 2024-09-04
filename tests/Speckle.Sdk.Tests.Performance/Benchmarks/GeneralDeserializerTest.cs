using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Toolchains.CsProj;
using Speckle.Objects.Geometry;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation;

namespace Speckle.Sdk.Tests.Performance.Benchmarks;

public static class JobExtensions
{
  public static Job WithConstants(this Job job, string constants) => job.WithArguments([new MsBuildArgument("/p:DefineConstants=\"" + constants + "\"")
  ]);
}
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

      var newJob = baseJob.WithNuGet("Speckle.Sdk", "3.0.1-rc.118").WithId("3.0.1-rc.118");
      var oldJob = baseJob.WithNuGet("Speckle.Sdk", "3.0.1-rc.117").WithId("3.0.1-rc.117");
      
      
      //AddJob(baseJob.WithNuGet("Speckle.Sdk", "3.1.0-dev.122").WithId("3.1.0-dev.122"));
      AddJob(newJob);
      AddJob(oldJob);
      //AddJob(oldJob.WithRuntime(CoreRuntime.Core80));
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
#if NEW_SERIALIZATION
  
  [Benchmark]
  public async Task<Base> RunTest()
  {
    SpeckleObjectDeserializer sut = new() { ReadTransport = _dataSource.Transport };
    return await sut.Deserialize(_dataSource.Transport.GetObject(_dataSource.ObjectId)!);
  }
#else
  [Benchmark]
  public Base RunTest()
  {
    BaseObjectDeserializerV2 sut = new() { ReadTransport = _dataSource.Transport };
    return sut.Deserialize(_dataSource.Transport.GetObject(_dataSource.ObjectId)!);
  }
#endif

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
