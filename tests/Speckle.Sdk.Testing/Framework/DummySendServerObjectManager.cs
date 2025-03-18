using System.Collections.Concurrent;
using Speckle.Sdk.Serialisation;
using Speckle.Sdk.Serialisation.V2;
using Speckle.Sdk.Serialisation.V2.Send;

namespace Speckle.Sdk.Testing.Framework;

public class DummySendServerObjectManager(ConcurrentDictionary<string, string> savedObjects) : IServerObjectManager
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
  ) => Task.FromResult(objectIds.Distinct().ToDictionary(x => x, _ => false));

  public Task UploadObjects(
    IReadOnlyList<BaseItem> objects,
    bool compressPayloads,
    IProgress<ProgressArgs>? progress,
    CancellationToken cancellationToken
  )
  {
    foreach (var obj in objects)
    {
      savedObjects.TryAdd(obj.Id.Value, obj.Json.Value);
    }
    return Task.CompletedTask;
  }
}
