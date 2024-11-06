using System.Collections.Concurrent;
using System.Text;
using NUnit.Framework;
using Shouldly;
using Speckle.Newtonsoft.Json.Linq;
using Speckle.Objects.Geometry;
using Speckle.Sdk.Dependencies.Serialization;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation;
using Speckle.Sdk.Serialisation.V2;
using Speckle.Sdk.Serialisation.V2.Send;
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

  [Fact]
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

    var objects = new Dictionary<string, string>();

    var process2 = new SerializeProcess(
      null,
      new DummySendCacheManager(objects),
      new DummyServerObjectManager(),
      new SpeckleBaseChildFinder(new SpeckleBasePropertyGatherer()),
      new SpeckleBasePropertyGatherer()
    );
    await process2
      .Serialize(string.Empty, @base, default, new SerializeProcessOptions(false, true));

    objects.Count.ShouldBe(2);
    objects.ContainsKey("9ff8efb13c62fa80f3d1c4519376ba13").ShouldBeTrue();
    objects.ContainsKey("d3dd4621b2f68c3058c2b9c023a9de19").ShouldBeTrue();
    JToken
      .DeepEquals(JObject.Parse(expectedJson), JObject.Parse(objects["9ff8efb13c62fa80f3d1c4519376ba13"]))
      .ShouldBeTrue();
    JToken
      .DeepEquals(JObject.Parse(detachedJson), JObject.Parse(objects["d3dd4621b2f68c3058c2b9c023a9de19"]))
      .ShouldBeTrue();
  }

  [Fact]
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

    objects.Count.ShouldBe(2);
    objects.ContainsKey("9ff8efb13c62fa80f3d1c4519376ba13").ShouldBeTrue();
    objects.ContainsKey("d3dd4621b2f68c3058c2b9c023a9de19").ShouldBeTrue();
    JToken.DeepEquals(JObject.Parse(json), JObject.Parse(objects["9ff8efb13c62fa80f3d1c4519376ba13"])).ShouldBeTrue();
    JToken
      .DeepEquals(JObject.Parse(expectedJson), JObject.Parse(objects["9ff8efb13c62fa80f3d1c4519376ba13"]))
      .ShouldBeTrue();
    JToken
      .DeepEquals(JObject.Parse(detachedJson), JObject.Parse(objects["d3dd4621b2f68c3058c2b9c023a9de19"]))
      .ShouldBeTrue();
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

    var children = new SpeckleBaseChildFinder(new SpeckleBasePropertyGatherer()).GetChildProperties(@base).ToList();

    children.Count.ShouldBe(4);
    children.First(x => x.Name == "detachedProp").PropertyAttributeInfo.IsDetachable.ShouldBeTrue();
    children.First(x => x.Name == "list").PropertyAttributeInfo.IsDetachable.ShouldBeTrue();
    children.First(x => x.Name == "arr").PropertyAttributeInfo.IsDetachable.ShouldBeTrue();
    children.First(x => x.Name == "@prop2").PropertyAttributeInfo.IsDetachable.ShouldBeTrue();
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

    var children = new SpeckleBasePropertyGatherer().ExtractAllProperties(@base).ToList();

    children.Count.ShouldBe(9);
    children.First(x => x.Name == "dynamicProp").PropertyAttributeInfo.IsDetachable.ShouldBeFalse();
    children.First(x => x.Name == "attachedProp").PropertyAttributeInfo.IsDetachable.ShouldBeFalse();
    children.First(x => x.Name == "crazyProp").PropertyAttributeInfo.IsDetachable.ShouldBeFalse();
    children.First(x => x.Name == "speckle_type").PropertyAttributeInfo.IsDetachable.ShouldBeFalse();
    children.First(x => x.Name == "applicationId").PropertyAttributeInfo.IsDetachable.ShouldBeFalse();

    children.First(x => x.Name == "detachedProp").PropertyAttributeInfo.IsDetachable.ShouldBeTrue();
    children.First(x => x.Name == "list").PropertyAttributeInfo.IsDetachable.ShouldBeTrue();
    children.First(x => x.Name == "arr").PropertyAttributeInfo.IsDetachable.ShouldBeTrue();
    children.First(x => x.Name == "@prop2").PropertyAttributeInfo.IsDetachable.ShouldBeTrue();
  }

  [Fact]
  public async Task CanSerialize_New_Detached2()
  {
    var root = """
         
      {
          "list": [],
          "arr": null,
          "detachedProp": {
              "speckle_type": "reference",
              "referencedId": "32a385e7ddeda810e037b21ab26381b7",
              "__closure": null
          },
          "detachedProp2": {
              "speckle_type": "reference",
              "referencedId": "c3858f47dd3e7a308a1b465375f1645f",
              "__closure": null
          },
          "attachedProp": {
              "name": "attachedProp",
              "line": {
                  "speckle_type": "reference",
                  "referencedId": "027a7c5ffcf8d8efe432899c729a954c",
                  "__closure": null
              },
              "applicationId": "4",
              "speckle_type": "Speckle.Core.Tests.Unit.Models.BaseTests+SamplePropBase2",
              "id": "c5dd540ee1299c0349829d045c04ef2d"
          },
          "crazyProp": null,
          "applicationId": "1",
          "speckle_type": "Speckle.Core.Tests.Unit.Models.BaseTests+SampleObjectBase2",
          "dynamicProp": 123,
          "id": "fd4efeb8a036838c53ad1cf9e82b8992",
          "__closure": {
              "8d27f5c7fac36d985d89bb6d6d8acddc": 3,
              "4ba53b5e84e956fb076bc8b0a03ca879": 2,
              "32a385e7ddeda810e037b21ab26381b7": 1,
              "1afc694774efa5913d0077302cd37888": 3,
              "045cbee36837d589b17f9d8483c90763": 2,
              "c3858f47dd3e7a308a1b465375f1645f": 1,
              "5b86b66b61c556ead500915b05852875": 2,
              "027a7c5ffcf8d8efe432899c729a954c": 1
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

    var process2 = new SerializeProcess(
      null,
      new DummySendCacheManager(objects),
      new DummyServerObjectManager(),
      new SpeckleBaseChildFinder(new SpeckleBasePropertyGatherer()),
      new SpeckleBasePropertyGatherer()
    );
    var results = await process2
        .Serialize(string.Empty, @base, default, new SerializeProcessOptions(false, true));

    objects.Count.ShouldBe(9);
    JToken.DeepEquals(JObject.Parse(root), JObject.Parse(objects["fd4efeb8a036838c53ad1cf9e82b8992"])).ShouldBeTrue();

    results.rootObjId.ShouldBe(@base.id);
    results.convertedReferences.Count.ShouldBe(2);
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
    string streamId,
    IReadOnlyList<string> objectIds,
    IProgress<ProgressArgs>? progress,
    CancellationToken cancellationToken
  ) => throw new NotImplementedException();

  public Task<string?> DownloadSingleObject(
    string streamId,
    string objectId,
    IProgress<ProgressArgs>? progress,
    CancellationToken cancellationToken
  ) => throw new NotImplementedException();

  public Task<Dictionary<string, bool>> HasObjects(
    string streamId,
    IReadOnlyList<string> objectIds,
    CancellationToken cancellationToken
  ) => throw new NotImplementedException();

  public Task UploadObjects(
    string streamId,
    IReadOnlyList<BaseItem> objects,
    bool compressPayloads,
    IProgress<ProgressArgs>? progress,
    CancellationToken cancellationToken
  )
  {
    long totalBytes = 0;
    foreach (var item in objects)
    {
      totalBytes += Encoding.Default.GetByteCount(item.Json);
    }

    progress?.Report(new(ProgressEvent.UploadBytes, totalBytes, totalBytes));
    return Task.CompletedTask;
  }
}

public class DummySendCacheManager(Dictionary<string, string> objects) : ISQLiteSendCacheManager
{
  public string? GetObject(string id) => null;

  public bool HasObject(string objectId) => false;

  public void SaveObjects(List<BaseItem> items)
  {
    foreach (var item in items)
    {
      objects.Add(item.Id, item.Json);
    }
  }
}
