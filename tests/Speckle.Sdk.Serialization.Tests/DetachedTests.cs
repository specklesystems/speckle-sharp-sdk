using System.Collections.Concurrent;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Speckle.Newtonsoft.Json.Linq;
using Speckle.Objects.Geometry;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation;
using Speckle.Sdk.Serialisation.V2;
using Speckle.Sdk.Serialisation.V2.Send;
using Speckle.Sdk.SQLite;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Serialization.Tests;

public class DetachedTests
{
  private readonly ISerializeProcessFactory _factory;

  public DetachedTests()
  {
    var serviceCollection = new ServiceCollection();
    serviceCollection.AddSpeckleSdk(new("Tests", "test"), "v3", typeof(TestClass).Assembly, typeof(Polyline).Assembly);
    var serviceProvider = serviceCollection.BuildServiceProvider();

    _factory = serviceProvider.GetRequiredService<ISerializeProcessFactory>();
  }

  [Fact]
  public async Task CanSerialize_New_Detached()
  {
    var @base = new SampleObjectBase();
    @base["dynamicProp"] = 123;
    @base.detachedProp = new SamplePropBase() { name = "detachedProp" };
    @base.attachedProp = new SamplePropBase() { name = "attachedProp" };

    var objects = new ConcurrentDictionary<string, string>();

    await using var serializeProcess = _factory.CreateSerializeProcess(
      new ConcurrentDictionary<Id, Json>(),
      objects,
      null,
      default,
      new SerializeProcessOptions(true, true, false, true)
    );
    await serializeProcess.Serialize(@base);

    await VerifyJsonDictionary(objects);
  }

  [Fact]
  public async Task CanSerialize_Old_Detached()
  {
    var @base = new SampleObjectBase();
    @base["dynamicProp"] = 123;
    @base.detachedProp = new SamplePropBase() { name = "detachedProp" };
    @base.attachedProp = new SamplePropBase() { name = "attachedProp" };

    var objects = new ConcurrentDictionary<string, string>();
    var serializer = new SpeckleObjectSerializer(new[] { new MemoryTransport(objects) });
    serializer.Serialize(@base);

    await VerifyJsonDictionary(objects);
  }

  [Fact]
  public async Task GetPropertiesExpected_Detached()
  {
    var @base = new SampleObjectBase();
    @base["dynamicProp"] = 123;
    @base["@prop2"] = 2;
    @base["__prop3"] = 3;
    @base.detachedProp = new SamplePropBase() { name = "detachedProp" };
    @base.attachedProp = new SamplePropBase() { name = "attachedProp" };

    var children = new BaseChildFinder(new BasePropertyGatherer()).GetChildProperties(@base).ToList();
    await Verify(children);
  }

  [Fact]
  public async Task GetPropertiesExpected_All()
  {
    var @base = new SampleObjectBase();
    @base["dynamicProp"] = 123;
    @base["@prop2"] = 2;
    @base["__prop3"] = 3;
    @base.detachedProp = new SamplePropBase() { name = "detachedProp" };
    @base.attachedProp = new SamplePropBase() { name = "attachedProp" };

    var children = new BasePropertyGatherer().ExtractAllProperties(@base).ToList();
    await Verify(children);
  }

  [Fact]
  public async Task CanSerialize_New_Detached2()
  {
    var @base = new SampleObjectBase2();
    @base["dynamicProp"] = 123;
    @base.applicationId = "1";
    @base.detachedProp = new SamplePropBase2()
    {
      name = "detachedProp",
      applicationId = "2",
      line = new Polyline() { units = "test", value = [1.0, 2.0] },
    };
    @base.detachedProp2 = new SamplePropBase2()
    {
      name = "detachedProp2",
      applicationId = "3",
      line = new Polyline() { units = "test", value = [3.0, 2.0] },
    };
    @base.attachedProp = new SamplePropBase2()
    {
      name = "attachedProp",
      applicationId = "4",
      line = new Polyline() { units = "test", value = [3.0, 4.0] },
    };

    var objects = new ConcurrentDictionary<string, string>();

    await using var serializeProcess = _factory.CreateSerializeProcess(
      new ConcurrentDictionary<Id, Json>(),
      objects,
      null,
      default,
      new SerializeProcessOptions(true, true, false, true) { MaxParallelism = 1, MaxHttpSendBatchSize = 1 }
    );
    var results = await serializeProcess.Serialize(@base);

    await VerifyJsonDictionary(objects);
  }

  [Fact]
  public async Task CanSerialize_Attached()
  {
    var @base = new SampleObjectBase2();
    @base["dynamicProp"] = 123;
    @base.applicationId = "1";
    @base.attachedProp = new SamplePropBase2()
    {
      name = "attachedProp",
      applicationId = "4",
      line = new Polyline() { units = "test", value = [3.0, 4.0] },
    };

    var objects = new ConcurrentDictionary<string, string>();

    await using var serializeProcess = _factory.CreateSerializeProcess(
      new ConcurrentDictionary<Id, Json>(),
      objects,
      null,
      default,
      new SerializeProcessOptions(true, true, false, true) { MaxParallelism = 1, MaxHttpSendBatchSize = 1 }
    );
    var results = await serializeProcess.Serialize(@base);

    await VerifyJsonDictionary(objects);
  }

  [Fact]
  public async Task CanSerialize_Attached_2()
  {
    var @base = new SampleObjectBase2();
    @base["dynamicProp"] = 123;
    @base.applicationId = "1";
    @base.attachedProp = new SamplePropBase2() { name = "attachedProp", applicationId = "4" };

    var objects = new ConcurrentDictionary<string, string>();

    await using var serializeProcess = _factory.CreateSerializeProcess(
      new ConcurrentDictionary<Id, Json>(),
      objects,
      null,
      default,
      new SerializeProcessOptions(true, true, false, true) { MaxParallelism = 1, MaxHttpSendBatchSize = 1 }
    );
    var results = await serializeProcess.Serialize(@base);

    await VerifyJsonDictionary(objects);
  }

  [Fact]
  public async Task CanSerialize_New_Detached_With_DataChunks()
  {
    var root = """
         {
        "list" : [ {
          "speckle_type" : "reference",
          "referencedId" : "0e61e61edee00404ec6e0f9f594bce24",
          "__closure" : null
        } ],
        "list2" : [ {
          "speckle_type" : "reference",
          "referencedId" : "f70738e3e3e593ac11099a6ed6b71154",
          "__closure" : null
        } ],
        "arr" : null,
        "detachedProp" : null,
        "detachedProp2" : null,
        "attachedProp" : null,
        "crazyProp" : null,
        "applicationId" : "1",
        "speckle_type" : "Speckle.Core.Tests.Unit.Models.BaseTests+SampleObjectBase2",
        "dynamicProp" : 123,
        "id" : "efeadaca70a85ae6d3acfc93a8b380db",
        "__closure" : {
          "0e61e61edee00404ec6e0f9f594bce24" : 1,
          "f70738e3e3e593ac11099a6ed6b71154" : 1
        }
      }
      """;

    var list1 = """
      {
        "data" : [ 1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0, 8.0, 9.0, 10.0 ],
        "applicationId" : null,
        "speckle_type" : "Speckle.Core.Models.DataChunk",
        "id" : "0e61e61edee00404ec6e0f9f594bce24"
      }
      """;
    var list2 = """
      {
        "data" : [ 1.0, 10.0 ],
        "applicationId" : null,
        "speckle_type" : "Speckle.Core.Models.DataChunk",
        "id" : "f70738e3e3e593ac11099a6ed6b71154"
      }
      """;
    var @base = new SampleObjectBase2();
    @base["dynamicProp"] = 123;
    @base.applicationId = "1";
    @base.list = new List<double>() { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
    @base.list2 = new List<double>() { 1, 10 };

    var objects = new ConcurrentDictionary<string, string>();

    await using var serializeProcess = _factory.CreateSerializeProcess(
      new ConcurrentDictionary<Id, Json>(),
      objects,
      null,
      default,
      new SerializeProcessOptions(true, true, false, true)
    );

    var results = await serializeProcess.Serialize(@base);

    objects.Count.Should().Be(3);
    var x = JObject.Parse(objects["efeadaca70a85ae6d3acfc93a8b380db"]);
    JToken.DeepEquals(JObject.Parse(root), x).Should().BeTrue();

    x = JObject.Parse(objects["0e61e61edee00404ec6e0f9f594bce24"]);
    JToken.DeepEquals(JObject.Parse(list1), x).Should().BeTrue();

    x = JObject.Parse(objects["f70738e3e3e593ac11099a6ed6b71154"]);
    JToken.DeepEquals(JObject.Parse(list2), x).Should().BeTrue();
  }

  [Fact]
  public async Task CanSerialize_New_Detached_With_DataChunks2()
  {
    var @base = new SampleObjectBase2();
    @base["dynamicProp"] = 123;
    @base.applicationId = "1";
    @base.list = new List<double>() { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
    @base.list2 = new List<double>() { 1, 10 };
    @base.arr = [1, 10];

    var objects = new ConcurrentDictionary<string, string>();

    await using var serializeProcess = _factory.CreateSerializeProcess(
      new ConcurrentDictionary<Id, Json>(),
      objects,
      null,
      default,
      new SerializeProcessOptions(true, true, false, true)
    );
    var results = await serializeProcess.Serialize(@base);
    await VerifyJsonDictionary(objects);
  }
}

[SpeckleType("Speckle.Core.Tests.Unit.Models.BaseTests+SampleObjectBase")]
public class SampleObjectBase : Base
{
  [Chunkable, DetachProperty]
  public List<double> list { get; set; } = new();

  [Chunkable(300), DetachProperty]
  public double[] arr { get; set; }

  [DetachProperty]
  public SamplePropBase detachedProp { get; set; }

  public SamplePropBase attachedProp { get; set; }

  public string crazyProp { get; set; }
}

[SpeckleType("Speckle.Core.Tests.Unit.Models.BaseTests+SamplePropBase")]
public class SamplePropBase : Base
{
  public string name { get; set; }
}

[SpeckleType("Speckle.Core.Tests.Unit.Models.BaseTests+SampleObjectBase2")]
public class SampleObjectBase2 : Base
{
  [Chunkable, DetachProperty]
  public List<double> list { get; set; } = new();

  [Chunkable, DetachProperty]
  public List<double> list2 { get; set; } = null!;

  [Chunkable(300), DetachProperty]
  public double[] arr { get; set; }

  [DetachProperty]
  public SamplePropBase2 detachedProp { get; set; }

  [DetachProperty]
  public SamplePropBase2 detachedProp2 { get; set; }

  public SamplePropBase2 attachedProp { get; set; }

  public string crazyProp { get; set; }
}

[SpeckleType("Speckle.Core.Tests.Unit.Models.BaseTests+SamplePropBase2")]
public class SamplePropBase2 : Base
{
  public string name { get; set; }

  [DetachProperty]
  public Polyline line { get; set; }
}

public class DummyServerObjectManager : IServerObjectManager
{
  public IAsyncEnumerable<(string, string)> DownloadObjects(
    IReadOnlyCollection<string> objectIds,
    IProgress<ProgressArgs>? progress,
    CancellationToken cancellationToken
  ) => throw new NotImplementedException();

  public virtual Task<string?> DownloadSingleObject(
    string objectId,
    IProgress<ProgressArgs>? progress,
    CancellationToken cancellationToken
  ) => throw new NotImplementedException();

  public Task<Dictionary<string, bool>> HasObjects(
    IReadOnlyCollection<string> objectIds,
    CancellationToken cancellationToken
  ) => Task.FromResult(objectIds.ToDictionary(x => x, _ => false));

  public virtual Task UploadObjects(
    IReadOnlyList<BaseItem> objects,
    bool compressPayloads,
    IProgress<ProgressArgs>? progress,
    CancellationToken cancellationToken
  )
  {
    long totalBytes = 0;
    foreach (var item in objects)
    {
      totalBytes += Encoding.Default.GetByteCount(item.Json.Value);
    }

    progress?.Report(new(ProgressEvent.UploadBytes, totalBytes, totalBytes));
    return Task.CompletedTask;
  }
}

public class DummySendCacheManager(Dictionary<string, string> objects) : ISqLiteJsonCacheManager
{
  public void Dispose() { }

  public IReadOnlyCollection<(string, string)> GetAllObjects() => throw new NotImplementedException();

  public void DeleteObject(string id) => throw new NotImplementedException();

  public string? GetObject(string id) => null;

  public void SaveObject(string id, string json) => throw new NotImplementedException();

  public void UpdateObject(string id, string json) => throw new NotImplementedException();

  public bool HasObject(string objectId) => false;

  public void SaveObjects(IEnumerable<(string id, string json)> items)
  {
    foreach (var (id, json) in items)
    {
      objects[id] = json;
    }
  }
}
