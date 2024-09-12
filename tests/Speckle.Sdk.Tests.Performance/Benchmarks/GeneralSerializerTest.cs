﻿using BenchmarkDotNet.Attributes;
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
  }

  [Benchmark]
  public async Task<string> RunTest()
  {
    var remote = new NullTransport();
    SpeckleObjectSerializer sut = new([remote]);
    var x = await sut.SerializeAsync(_testData);
    return x;
  }
}

public class NullTransport : ITransport
{
  public string TransportName { get; set; } = "";
  public Dictionary<string, object> TransportContext { get; } = new();
  public TimeSpan Elapsed { get; } = TimeSpan.Zero;
  public CancellationToken CancellationToken { get; set; }
  public Action<ProgressArgs> OnProgressAction { get; set; }

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