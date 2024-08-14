namespace Speckle.Sdk.Transports.ServerUtils;

public delegate void CbObjectDownloaded(string id, string json);
public delegate void CbBlobdDownloaded();

internal interface IServerApi
{
  public Task<string?> DownloadSingleObject(string streamId, string objectId, Action<ProgressArgs>? progress);

  public Task DownloadObjects(
    string streamId,
    IReadOnlyList<string> objectIds,
    Action<ProgressArgs>? progress,
    CbObjectDownloaded onObjectCallback
  );

  public Task<Dictionary<string, bool>> HasObjects(string streamId, IReadOnlyList<string> objectIds);

  public Task UploadObjects(
    string streamId,
    IReadOnlyList<(string id, string data)> objects,
    Action<ProgressArgs>? progress
  );

  public Task UploadBlobs(
    string streamId,
    IReadOnlyList<(string id, string data)> objects,
    Action<ProgressArgs>? progress
  );

  public Task DownloadBlobs(
    string streamId,
    IReadOnlyList<string> blobIds,
    CbBlobdDownloaded onBlobCallback,
    Action<ProgressArgs>? progress
  );
}
