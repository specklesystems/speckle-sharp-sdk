using System.Drawing;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation;
using Point = Speckle.Objects.Geometry.Point;

namespace Speckle.Sdk.Tests.Performance.Benchmarks;

/// <summary>
/// How many threads on our Deserializer is optimal
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Monitoring)]
public class DeserializationWorkerThreads : IDisposable
{
  public static IEnumerable<int> NumThreadsToTest => Enumerable.Range(0, Environment.ProcessorCount + 1);

  [Params(0, 9)]
  public int DataComplexity { get; set; }

  private TestDataHelper _dataSource;

  [GlobalSetup]
  public async Task Setup()
  {
    TypeLoader.Initialize(typeof(Base).Assembly, typeof(Point).Assembly);
    _dataSource = new TestDataHelper();
    await _dataSource.SeedTransport(DataComplexity).ConfigureAwait(false);
  }

  [Benchmark]
  [ArgumentsSource(nameof(NumThreadsToTest))]
  public Base RunTest(int numThreads)
  {
    BaseObjectDeserializerV2 sut = new() { WorkerThreadCount = numThreads, ReadTransport = _dataSource.Transport };
    return sut.Deserialize(_dataSource.Transport.GetObject(_dataSource.ObjectId)!);
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
