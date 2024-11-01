using System.Collections.Concurrent;
using Shouldly;
using Speckle.Newtonsoft.Json.Linq;
using Speckle.Sdk.Common;
using Speckle.Sdk.Dependencies.Serialization;
using Speckle.Sdk.Serialisation.V2;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Serialization.Tests;

public class DummySendServerObjectManager(ConcurrentDictionary<string, string> savedObjects) : IServerObjectManager
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
  )
  {
    return Task.FromResult(objectIds.ToDictionary(x => x, x => false));
  }

  public Task UploadObjects(
    string streamId,
    IReadOnlyList<BaseItem> objects,
    bool compressPayloads,
    IProgress<ProgressArgs>? progress,
    CancellationToken cancellationToken
  )
  {
    foreach (var obj in objects)
    {
      obj.Id.ShouldBe(JObject.Parse(obj.Json)["id"].NotNull().Value<string>());
      if (savedObjects.TryGetValue(obj.Id, out var j))
      {
        j.ShouldBe(obj.Json);
      }
      else
      {
        savedObjects.TryAdd(obj.Id, obj.Json);
      }
    }
    return Task.CompletedTask;
  }
}
