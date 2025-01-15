using System.Collections.Concurrent;
using System.Text;
using FluentAssertions;
using Speckle.Newtonsoft.Json.Linq;
using Speckle.Objects.Geometry;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation;
using Speckle.Sdk.Serialisation.V2;
using Speckle.Sdk.Serialisation.V2.Send;
using Speckle.Sdk.SQLite;
using Speckle.Sdk.Transports;
using Xunit;

namespace Speckle.Sdk.Serialization.Tests;

public class DetachedTests
{
  public DetachedTests()
  {
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(Base).Assembly, typeof(DetachedTests).Assembly, typeof(Polyline).Assembly);
  }

  [Fact(DisplayName = "Checks that all typed properties (including obsolete ones) are returned")]
  public async Task CanSerialize_New_Detached()
  {
    var expectedJson = """
      {
          "list": [],
          "arr": null,
          "detachedProp": {
              "speckle_type": "reference",
              "referencedId": "d3dd4621b2f68c3058c2b9c023a9de19",
              "__closure": null
          },
          "attachedProp": {
              "name": "attachedProp",
              "applicationId": null,
              "speckle_type": "Speckle.Core.Tests.Unit.Models.BaseTests+SamplePropBase",
              "id": "90d58b65c9036a8bc50743f4c71c1c2e"
          },
          "crazyProp": null,
          "applicationId": null,
          "speckle_type": "Speckle.Core.Tests.Unit.Models.BaseTests+SampleObjectBase",
          "dynamicProp": 123,
          "id": "9ff8efb13c62fa80f3d1c4519376ba13",
          "__closure": {
              "d3dd4621b2f68c3058c2b9c023a9de19": 100
          }
      }
      """;
    var detachedJson = """
      {
          "name": "detachedProp",
          "applicationId": null,
          "speckle_type": "Speckle.Core.Tests.Unit.Models.BaseTests+SamplePropBase",
          "id": "d3dd4621b2f68c3058c2b9c023a9de19"
      }
      """;
    var @base = new SampleObjectBase();
    @base["dynamicProp"] = 123;
    @base.detachedProp = new SamplePropBase() { name = "detachedProp" };
    @base.attachedProp = new SamplePropBase() { name = "attachedProp" };

    var objects = new Dictionary<string, string>();

    using var process2 = new SerializeProcess(
      null,
      new DummySendCacheManager(objects),
      new DummyServerObjectManager(),
      new BaseChildFinder(new BasePropertyGatherer()),
      new ObjectSerializerFactory(new BasePropertyGatherer()),
      new SerializeProcessOptions(false, false, true, true)
    );
    await process2.Serialize(@base, default);

    objects.Count.Should().Be(2);
    objects.ContainsKey("9ff8efb13c62fa80f3d1c4519376ba13").Should().BeTrue();
    objects.ContainsKey("d3dd4621b2f68c3058c2b9c023a9de19").Should().BeTrue();
    JToken
      .DeepEquals(JObject.Parse(expectedJson), JObject.Parse(objects["9ff8efb13c62fa80f3d1c4519376ba13"]))
      .Should()
      .BeTrue();
    JToken
      .DeepEquals(JObject.Parse(detachedJson), JObject.Parse(objects["d3dd4621b2f68c3058c2b9c023a9de19"]))
      .Should()
      .BeTrue();
  }

  [Fact(DisplayName = "Checks that all typed properties (including obsolete ones) are returned")]
  public void CanSerialize_Old_Detached()
  {
    var expectedJson = """
      {
          "list": [],
          "arr": null,
          "detachedProp": {
              "speckle_type": "reference",
              "referencedId": "d3dd4621b2f68c3058c2b9c023a9de19",
              "__closure": null
          },
          "attachedProp": {
              "name": "attachedProp",
              "applicationId": null,
              "speckle_type": "Speckle.Core.Tests.Unit.Models.BaseTests+SamplePropBase",
              "id": "90d58b65c9036a8bc50743f4c71c1c2e"
          },
          "crazyProp": null,
          "applicationId": null,
          "speckle_type": "Speckle.Core.Tests.Unit.Models.BaseTests+SampleObjectBase",
          "dynamicProp": 123,
          "id": "9ff8efb13c62fa80f3d1c4519376ba13",
          "__closure": {
              "d3dd4621b2f68c3058c2b9c023a9de19": 1
          }
      }
      """;
    var detachedJson = """
      {
          "name": "detachedProp",
          "applicationId": null,
          "speckle_type": "Speckle.Core.Tests.Unit.Models.BaseTests+SamplePropBase",
          "id": "d3dd4621b2f68c3058c2b9c023a9de19"
      }
      """;
    var @base = new SampleObjectBase();
    @base["dynamicProp"] = 123;
    @base.detachedProp = new SamplePropBase() { name = "detachedProp" };
    @base.attachedProp = new SamplePropBase() { name = "attachedProp" };

    var objects = new ConcurrentDictionary<string, string>();
    var serializer = new SpeckleObjectSerializer(new[] { new MemoryTransport(objects) });
    var json = serializer.Serialize(@base);

    objects.Count.Should().Be(2);
    objects.ContainsKey("9ff8efb13c62fa80f3d1c4519376ba13").Should().BeTrue();
    objects.ContainsKey("d3dd4621b2f68c3058c2b9c023a9de19").Should().BeTrue();
    JToken
      .DeepEquals(JObject.Parse(json), JObject.Parse(objects["9ff8efb13c62fa80f3d1c4519376ba13"]))
      .Should()
      .BeTrue();
    JToken
      .DeepEquals(JObject.Parse(expectedJson), JObject.Parse(objects["9ff8efb13c62fa80f3d1c4519376ba13"]))
      .Should()
      .BeTrue();
    JToken
      .DeepEquals(JObject.Parse(detachedJson), JObject.Parse(objects["d3dd4621b2f68c3058c2b9c023a9de19"]))
      .Should()
      .BeTrue();
  }

  [Fact]
  public void GetPropertiesExpected_Detached()
  {
    var @base = new SampleObjectBase();
    @base["dynamicProp"] = 123;
    @base["@prop2"] = 2;
    @base["__prop3"] = 3;
    @base.detachedProp = new SamplePropBase() { name = "detachedProp" };
    @base.attachedProp = new SamplePropBase() { name = "attachedProp" };

    var children = new BaseChildFinder(new BasePropertyGatherer()).GetChildProperties(@base).ToList();

    children.Count.Should().Be(4);
    children.First(x => x.Name == "detachedProp").PropertyAttributeInfo.IsDetachable.Should().BeTrue();
    children.First(x => x.Name == "list").PropertyAttributeInfo.IsDetachable.Should().BeTrue();
    children.First(x => x.Name == "arr").PropertyAttributeInfo.IsDetachable.Should().BeTrue();
    children.First(x => x.Name == "@prop2").PropertyAttributeInfo.IsDetachable.Should().BeTrue();
  }

  [Fact]
  public void GetPropertiesExpected_All()
  {
    var @base = new SampleObjectBase();
    @base["dynamicProp"] = 123;
    @base["@prop2"] = 2;
    @base["__prop3"] = 3;
    @base.detachedProp = new SamplePropBase() { name = "detachedProp" };
    @base.attachedProp = new SamplePropBase() { name = "attachedProp" };

    var children = new BasePropertyGatherer().ExtractAllProperties(@base).ToList();

    children.Count.Should().Be(9);
    children.First(x => x.Name == "dynamicProp").PropertyAttributeInfo.IsDetachable.Should().BeFalse();
    children.First(x => x.Name == "attachedProp").PropertyAttributeInfo.IsDetachable.Should().BeFalse();
    children.First(x => x.Name == "crazyProp").PropertyAttributeInfo.IsDetachable.Should().BeFalse();
    children.First(x => x.Name == "speckle_type").PropertyAttributeInfo.IsDetachable.Should().BeFalse();
    children.First(x => x.Name == "applicationId").PropertyAttributeInfo.IsDetachable.Should().BeFalse();

    children.First(x => x.Name == "detachedProp").PropertyAttributeInfo.IsDetachable.Should().BeTrue();
    children.First(x => x.Name == "list").PropertyAttributeInfo.IsDetachable.Should().BeTrue();
    children.First(x => x.Name == "arr").PropertyAttributeInfo.IsDetachable.Should().BeTrue();
    children.First(x => x.Name == "@prop2").PropertyAttributeInfo.IsDetachable.Should().BeTrue();
  }

  [Fact(DisplayName = "Checks that all typed properties (including obsolete ones) are returned")]
  public async Task CanSerialize_New_Detached2()
  {
    var root = """
      {
        "list" : [ ],
        "list2" : null,
        "arr" : null,
        "detachedProp" : {
          "speckle_type" : "reference",
          "referencedId" : "32a385e7ddeda810e037b21ab26381b7",
          "__closure" : null
        },
        "detachedProp2" : {
          "speckle_type" : "reference",
          "referencedId" : "c3858f47dd3e7a308a1b465375f1645f",
          "__closure" : null
        },
        "attachedProp" : {
          "name" : "attachedProp",
          "line" : {
            "speckle_type" : "reference",
            "referencedId" : "027a7c5ffcf8d8efe432899c729a954c",
            "__closure" : null
          },
          "applicationId" : "4",
          "speckle_type" : "Speckle.Core.Tests.Unit.Models.BaseTests+SamplePropBase2",
          "id" : "c5dd540ee1299c0349829d045c04ef2d"
        },
        "crazyProp" : null,
        "applicationId" : "1",
        "speckle_type" : "Speckle.Core.Tests.Unit.Models.BaseTests+SampleObjectBase2",
        "dynamicProp" : 123,
        "id" : "2ebfd4f317754fce14cadd001151441e",
        "__closure" : {
          "8d27f5c7fac36d985d89bb6d6d8acddc" : 100,
          "4ba53b5e84e956fb076bc8b0a03ca879" : 100,
          "32a385e7ddeda810e037b21ab26381b7" : 100,
          "1afc694774efa5913d0077302cd37888" : 100,
          "045cbee36837d589b17f9d8483c90763" : 100,
          "c3858f47dd3e7a308a1b465375f1645f" : 100,
          "5b86b66b61c556ead500915b05852875" : 100,
          "027a7c5ffcf8d8efe432899c729a954c" : 100
        }
      }
      """;
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

    var objects = new Dictionary<string, string>();

    using var process2 = new SerializeProcess(
      null,
      new DummySendCacheManager(objects),
      new DummyServerObjectManager(),
      new BaseChildFinder(new BasePropertyGatherer()),
      new ObjectSerializerFactory(new BasePropertyGatherer()),
      new SerializeProcessOptions(false, false, true, true)
    );
    var results = await process2.Serialize(@base, default);

    objects.Count.Should().Be(9);
    var x = JObject.Parse(objects["2ebfd4f317754fce14cadd001151441e"]);
    JToken.DeepEquals(JObject.Parse(root), x).Should().BeTrue();

    results.RootId.Should().Be(@base.id);
    results.ConvertedReferences.Count.Should().Be(2);
  }

  [Fact(DisplayName = "Checks that all typed properties (including obsolete ones) are returned")]
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
          "0e61e61edee00404ec6e0f9f594bce24" : 100,
          "f70738e3e3e593ac11099a6ed6b71154" : 100
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

    var objects = new Dictionary<string, string>();

    using var process2 = new SerializeProcess(
      null,
      new DummySendCacheManager(objects),
      new DummyServerObjectManager(),
      new BaseChildFinder(new BasePropertyGatherer()),
      new ObjectSerializerFactory(new BasePropertyGatherer()),
      new SerializeProcessOptions(false, false, true, true)
    );
    var results = await process2.Serialize(@base, default);

    objects.Count.Should().Be(3);
    var x = JObject.Parse(objects["efeadaca70a85ae6d3acfc93a8b380db"]);
    JToken.DeepEquals(JObject.Parse(root), x).Should().BeTrue();

    x = JObject.Parse(objects["0e61e61edee00404ec6e0f9f594bce24"]);
    JToken.DeepEquals(JObject.Parse(list1), x).Should().BeTrue();

    x = JObject.Parse(objects["f70738e3e3e593ac11099a6ed6b71154"]);
    JToken.DeepEquals(JObject.Parse(list2), x).Should().BeTrue();
  }

  [Fact(DisplayName = "Checks that all typed properties (including obsolete ones) are returned")]
  public async Task CanSerialize_New_Detached_With_DataChunks2()
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
        "arr" : [ {
          "speckle_type" : "reference",
          "referencedId" : "f70738e3e3e593ac11099a6ed6b71154",
          "__closure" : null
        } ],
        "detachedProp" : null,
        "detachedProp2" : null,
        "attachedProp" : null,
        "crazyProp" : null,
        "applicationId" : "1",
        "speckle_type" : "Speckle.Core.Tests.Unit.Models.BaseTests+SampleObjectBase2",
        "dynamicProp" : 123,
        "id" : "525b1e9eef4d07165abb4ffc518395fc",
        "__closure" : {
          "0e61e61edee00404ec6e0f9f594bce24" : 100,
          "f70738e3e3e593ac11099a6ed6b71154" : 100
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
    @base.arr = [1, 10];

    var objects = new Dictionary<string, string>();

    using var process2 = new SerializeProcess(
      null,
      new DummySendCacheManager(objects),
      new DummyServerObjectManager(),
      new BaseChildFinder(new BasePropertyGatherer()),
      new ObjectSerializerFactory(new BasePropertyGatherer()),
      new SerializeProcessOptions(false, false, true, true)
    );
    var results = await process2.Serialize(@base, default);

    objects.Count.Should().Be(3);
    var x = JObject.Parse(objects["525b1e9eef4d07165abb4ffc518395fc"]);
    JToken.DeepEquals(JObject.Parse(root), x).Should().BeTrue();

    x = JObject.Parse(objects["0e61e61edee00404ec6e0f9f594bce24"]);
    JToken.DeepEquals(JObject.Parse(list1), x).Should().BeTrue();

    x = JObject.Parse(objects["f70738e3e3e593ac11099a6ed6b71154"]);
    JToken.DeepEquals(JObject.Parse(list2), x).Should().BeTrue();
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

  public Task<string?> DownloadSingleObject(
    string objectId,
    IProgress<ProgressArgs>? progress,
    CancellationToken cancellationToken
  ) => throw new NotImplementedException();

  public Task<Dictionary<string, bool>> HasObjects(
    IReadOnlyCollection<string> objectIds,
    CancellationToken cancellationToken
  ) => throw new NotImplementedException();

  public Task UploadObjects(
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
  public IReadOnlyCollection<(string Id, string Json)> GetObjects(string[] ids) => throw new NotImplementedException();

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
