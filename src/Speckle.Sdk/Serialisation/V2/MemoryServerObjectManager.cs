using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Speckle.Sdk.Serialisation.V2.Send;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Serialisation.V2;

public class MemoryServerObjectManager(ConcurrentDictionary<string, string> objects) : IServerObjectManager
{
  public virtual async IAsyncEnumerable<(string, string)> DownloadObjects(
    IReadOnlyCollection<string> objectIds,
    IProgress<ProgressArgs>? progress,
    [EnumeratorCancellation] CancellationToken cancellationToken
  )
  {
    foreach (var item in objects.Where(x => objectIds.Contains(x.Key)))
    {
      cancellationToken.ThrowIfCancellationRequested();
      yield return (item.Key, item.Value);
    }
    await Task.CompletedTask.ConfigureAwait(false);
  }

  public virtual Task<string?> DownloadSingleObject(
    string objectId,
    IProgress<ProgressArgs>? progress,
    CancellationToken cancellationToken
  ) => Task.FromResult(objects.TryGetValue(objectId, out var json) ? json : null);

  public virtual Task<Dictionary<string, bool>> HasObjects(
    IReadOnlyCollection<string> objectIds,
    CancellationToken cancellationToken
  ) => Task.FromResult(objectIds.ToDictionary(x => x, objects.ContainsKey));

  public virtual Task UploadObjects(
    IReadOnlyList<BaseItem> objectToUpload,
    bool compressPayloads,
    IProgress<ProgressArgs>? progress,
    CancellationToken cancellationToken
  )
  {
    foreach (BaseItem baseItem in objectToUpload)
    {
      objects.TryAdd(baseItem.Id.Value, baseItem.Json.Value);
    }
    return Task.CompletedTask;
  }
}
