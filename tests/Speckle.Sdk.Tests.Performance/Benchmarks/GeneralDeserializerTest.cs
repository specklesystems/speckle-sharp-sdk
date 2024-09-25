using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Speckle.Objects.Geometry;
using Speckle.Sdk.Common;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation;
using Speckle.Sdk.Serialisation.Receive;
using Speckle.Sdk.Serialisation.Send;
using Speckle.Sdk.Serialisation.Utilities;

namespace Speckle.Sdk.Tests.Performance.Benchmarks;

/// <summary>
/// How many threads on our Deserializer is optimal
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Monitoring, 0, 0, 4)]
public class GeneralDeserializer : IDisposable
{
  private TestDataHelper _dataSource;

  [GlobalSetup]
  public async Task Setup()
  {
    TypeLoader.Initialize(typeof(Base).Assembly, typeof(Point).Assembly);

    var serviceCollection = new ServiceCollection();
    serviceCollection.AddSpeckleSdk(HostApplications.Navisworks, HostAppVersion.v2023, "Test");
    var serviceProvider = serviceCollection.BuildServiceProvider();

    _dataSource = ActivatorUtilities.GetServiceOrCreateInstance<TestDataHelper>(serviceProvider);

    await _dataSource
      .SeedTransport(new StreamWrapper("https://latest.speckle.systems/projects/2099ac4b5f/models/da511c4d1e"))
      .ConfigureAwait(false);
  }

  [Benchmark]
  public async Task<Base> SpeckleObjectDeserializer2()
  {
    var rootObject = await _dataSource.Transport.GetObject(_dataSource.ObjectId).NotNull();
    var ids = ClosureParser.GetClosures(rootObject).OrderByDescending(x => x.depth);

    Dictionary<string, Base> deserializedObjects = new();
    SpeckleObjectDeserializer2 sut = new(deserializedObjects, SpeckleObjectSerializer2Pool.Instance);

    foreach (var (id, depth) in ids)
    {
      //Doesn't work because depth can't be trusted, In channels we re-queue, but this could be the source of inefficiency especially if we aren't using the sqlite cache
      var json = await _dataSource.Transport.GetObject(id).NotNull();
      var res = sut.Deserialize(json);
      deserializedObjects.Add(id, res);
    }

    var rootObjectJson = await _dataSource.Transport.GetObject(_dataSource.ObjectId).NotNull();
    return sut.Deserialize(rootObjectJson);
  }

  // [Benchmark]
  public async Task<Base> SpeckleObjectDeserializer()
  {
    SpeckleObjectDeserializer sut = new() { ReadTransport = _dataSource.Transport };
    string data = await _dataSource.Transport.GetObject(_dataSource.ObjectId).NotNull();
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
