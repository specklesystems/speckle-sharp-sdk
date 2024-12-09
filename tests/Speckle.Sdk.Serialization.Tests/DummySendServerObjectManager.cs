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
    IReadOnlyList<string> objectIds,
    IProgress<ProgressArgs>? progress,
    CancellationToken cancellationToken
  ) => throw new NotImplementedException();

  public Task<string?> DownloadSingleObject(
    string objectId,
    IProgress<ProgressArgs>? progress,
    CancellationToken cancellationToken
  ) => throw new NotImplementedException();

  public Task<Dictionary<string, bool>> HasObjects(IReadOnlyList<string> objectIds, CancellationToken cancellationToken)
  {
    return Task.FromResult(objectIds.ToDictionary(x => x, x => false));
  }

  public Task UploadObjects(
    IReadOnlyList<BaseItem> objects,
    bool compressPayloads,
    IProgress<ProgressArgs>? progress,
    CancellationToken cancellationToken
  )
  {
    foreach (var obj in objects)
    {
      savedObjects.TryAdd(obj.Id, obj.Json);
    }
    return Task.CompletedTask;
  }
}
