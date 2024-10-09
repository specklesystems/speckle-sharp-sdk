namespace Speckle.Sdk.Transports.ServerUtils;

public delegate void CbObjectDownloaded(string id, string json);

internal interface IServerApi
{
  public Task<string?> DownloadSingleObject(string streamId, string objectId, IProgress<ProgressArgs>? progress);

  public Task DownloadObjects(
    string streamId,
    IReadOnlyList<string> objectIds,
    IProgress<ProgressArgs>? progress,
    CbObjectDownloaded onObjectCallback
  );

  public Task<Dictionary<string, bool>> HasObjects(string streamId, IReadOnlyList<string> objectIds);

  public Task UploadObjects(
    string streamId,
    IReadOnlyList<(string id, string data)> objects,
    IProgress<ProgressArgs>? progress
  );

  public Task UploadBlobs(
    string streamId,
    IReadOnlyList<(string id, string data)> objects,
    IProgress<ProgressArgs>? progress
  );

  public Task DownloadBlobs(string streamId, IReadOnlyList<string> blobIds, IProgress<ProgressArgs>? progress);
}
