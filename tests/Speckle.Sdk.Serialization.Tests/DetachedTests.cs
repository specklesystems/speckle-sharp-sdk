using System.Collections.Concurrent;
using System.Text;
using NUnit.Framework;
using Shouldly;
using Speckle.Newtonsoft.Json.Linq;
using Speckle.Sdk.Dependencies.Serialization;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation;
using Speckle.Sdk.Serialisation.V2;
using Speckle.Sdk.Serialisation.V2.Send;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Serialization.Tests;

public class DetachedTests
{
  [Test(Description = "Checks that all typed properties (including obsolete ones) are returned")]
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
      .Serialize(string.Empty, @base, default, new SerializeProcessOptions(false, true))
      .ConfigureAwait(false);

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

  [Test(Description = "Checks that all typed properties (including obsolete ones) are returned")]
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
