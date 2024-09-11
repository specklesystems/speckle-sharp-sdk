using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Speckle.Objects.Geometry;
using Speckle.Sdk.Common;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Tests.Performance.Benchmarks;

/// <summary>
/// How many threads on our Deserializer is optimal
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Monitoring)]
public class GeneralSerializerTest
{
  private Base _testData;
  private ITransport _remote;

  [GlobalSetup]
  public async Task Setup()
  {
    TypeLoader.Initialize(typeof(Base).Assembly, typeof(Point).Assembly);
    using var dataSource = new TestDataHelper();
    await dataSource
      .SeedTransport(new("https://latest.speckle.systems/projects/2099ac4b5f/models/da511c4d1e"))
      .ConfigureAwait(false);

    SpeckleObjectDeserializer deserializer = new() { ReadTransport = dataSource.Transport };
    string data = await dataSource.Transport.GetObject(dataSource.ObjectId).NotNull();
    _testData = await deserializer.DeserializeJsonAsync(data).NotNull();
    _remote = new MemoryTransport();
  }

  [Benchmark]
  public string RunTest()
  {
    SpeckleObjectSerializer sut = new([_remote]);
    Console.ReadLine();
    var x =  sut.Serialize(_testData);
    Console.ReadLine();
    return x;
  }
}
