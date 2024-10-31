using System.Text;
using Speckle.Sdk.Dependencies.Serialization;
using Speckle.Sdk.Serialisation.V2;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Serialization.Testing;

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
