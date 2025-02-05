using System.Runtime.CompilerServices;
using System.Text;
using Speckle.Sdk.Serialisation.V2;
using Speckle.Sdk.Serialisation.V2.Send;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Testing.Framework;

public class DummyReceiveServerObjectManager(IReadOnlyDictionary<string, string> objects) : IServerObjectManager
{
  public async IAsyncEnumerable<(string, string)> DownloadObjects(
    IReadOnlyCollection<string> objectIds,
    IProgress<ProgressArgs>? progress,
    [EnumeratorCancellation] CancellationToken cancellationToken
  )
  {
    await Task.CompletedTask;
    foreach (var id in objectIds)
    {
      yield return (id, objects[id]);
    }
  }

  public async Task<string?> DownloadSingleObject(
    string objectId,
    IProgress<ProgressArgs>? progress,
    CancellationToken cancellationToken
  )
  {
    await Task.CompletedTask;
    return objects[objectId];
  }

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
