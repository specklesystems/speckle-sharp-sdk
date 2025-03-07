using System.Text;
using Speckle.Sdk.Serialisation.V2.Send;
using Speckle.Sdk.SQLite;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Serialisation.V2;

#pragma warning disable CA1063
public class DummySqLiteJsonCacheManager : ISqLiteJsonCacheManager
#pragma warning restore CA1063
{
#pragma warning disable CA1816
#pragma warning disable CA1063
  public void Dispose() { }
#pragma warning restore CA1063
#pragma warning restore CA1816

  public IReadOnlyCollection<(string, string)> GetAllObjects() => throw new NotImplementedException();

  public void DeleteObject(string id) => throw new NotImplementedException();

  public string? GetObject(string id) => null;

  public void SaveObject(string id, string json) => throw new NotImplementedException();

  public void UpdateObject(string id, string json) => throw new NotImplementedException();

  public virtual void SaveObjects(IEnumerable<(string id, string json)> items) => throw new NotImplementedException();

  public bool HasObject(string objectId) => false;
}

public class DummySendServerObjectManager : IServerObjectManager
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
