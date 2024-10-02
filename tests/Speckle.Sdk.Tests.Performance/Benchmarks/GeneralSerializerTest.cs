using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Microsoft.Extensions.Logging.Abstractions;
using Speckle.Objects.Geometry;
using Speckle.Sdk.Common;
using Speckle.Sdk.Credentials;
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

  [GlobalSetup]
  public async Task Setup()
  {
    TypeLoader.Initialize(typeof(Base).Assembly, typeof(Point).Assembly);
    using var dataSource = new TestDataHelper();
    await dataSource
      .SeedTransport(
        new Account()
        {
          serverInfo = new() { url = "https://latest.speckle.systems/projects/2099ac4b5f/models/da511c4d1e" }
        },
        "2099ac4b5f",
        "30fb4cbe6eb2202b9e7b4a4fcc3dd2b6"
      )
      .ConfigureAwait(false);

    SpeckleObjectDeserializer deserializer = new() { ReadTransport = dataSource.Transport };
    string data = await dataSource.Transport.GetObject(dataSource.ObjectId).NotNull();
    _testData = await deserializer.DeserializeAsync(data).NotNull();
  }

  [Benchmark]
  public string RunTest()
  {
    var remote = new NullTransport();
    SpeckleObjectSerializer sut = new([remote]);
    var x = sut.Serialize(_testData);
    return x;
  }
}

public class NullTransport : ITransport
{
  public string TransportName { get; set; } = "";
  public Dictionary<string, object> TransportContext { get; } = new();
  public TimeSpan Elapsed { get; } = TimeSpan.Zero;
  public CancellationToken CancellationToken { get; set; }
  public IProgress<ProgressArgs> OnProgressAction { get; set; }

  public void BeginWrite() { }

  public void EndWrite() { }

  public void SaveObject(string id, string serializedObject) { }

  public Task WriteComplete()
  {
    return Task.CompletedTask;
  }

  public Task<string> GetObject(string id) => throw new NotImplementedException();

  public Task<string> CopyObjectAndChildren(
    string id,
    ITransport targetTransport,
    Action<int> onTotalChildrenCountKnown = null
  ) => throw new NotImplementedException();

  public Task<Dictionary<string, bool>> HasObjects(IReadOnlyList<string> objectIds) =>
    throw new NotImplementedException();
}
