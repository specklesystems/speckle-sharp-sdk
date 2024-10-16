using Speckle.Sdk.Common;
using Speckle.Sdk.Helpers;
using Speckle.Sdk.Logging;
using Speckle.Sdk.Serialisation.Utilities;
using Speckle.Sdk.Transports;
using Speckle.Sdk.Transports.ServerUtils;

namespace Speckle.Sdk.Serialisation.Receive;

public interface IObjectLoader
{
  Task<(string, List<string>)> DownloadAndLoad(string rootId, CancellationToken cancellationToken);
}
public sealed class ObjectLoader(ISpeckleHttp http,
  ISdkActivityFactory activityFactory,
  Uri serverUrl,
  string streamId,
  string? token,
   IProgress<ProgressArgs>? progress,
     SQLiteTransport transport) : IObjectLoader, IDisposable
{
  private const int HTTP_ID_CHUNK_SIZE = 50;
  private const int CACHE_CHUNK_SIZE = 500;
  private readonly ServerApi _api = new(http, activityFactory, serverUrl, token, string.Empty);
  
  private async Task<string> GetRootJson(string objectId)
  {
    var rootJson = await transport.GetObject(objectId).ConfigureAwait(false);
    if (rootJson == null)
    {
      rootJson = await _api.DownloadSingleObject(streamId, objectId, progress).NotNull().ConfigureAwait(false);
      transport.SaveObjectSync(objectId, rootJson);
    }

    return rootJson;
  }

  public async Task<(string, List<string>)> DownloadAndLoad(string rootId, CancellationToken cancellationToken)
  {
    var rootJson = await GetRootJson(rootId).ConfigureAwait(false);
    var childrenIds = ClosureParser.GetClosures(rootJson).OrderByDescending(x => x.Item2).Select(x => x.Item1).ToList();
    var nonCachedChildIds = childrenIds
      .Where(x => !x.StartsWith("blob", StringComparison.Ordinal))
      .Batch(CACHE_CHUNK_SIZE) //review this after putting in sqlite cache
      .Select(x => CheckCache(x))
      .SelectManyAsync()
      .BatchAsync(HTTP_ID_CHUNK_SIZE);
    
    var count = 0L;
    progress?.Report(new (ProgressEvent.DownloadObject, count, childrenIds.Count));
    var downloads = new List<IAsyncEnumerable<(string, string)>>();
    await foreach (var batch in nonCachedChildIds)
    {
      downloads.Add(_api.DownloadObjectsImpl2(streamId, batch, progress));
    }

    var toCache = new List<(string, string)>();
    var tasks = new List<Task>();
    await foreach (var (id, json) in downloads.SelectManyAsync().WithCancellation(cancellationToken))
    {
      count++;
      progress?.Report(new (ProgressEvent.DownloadObject, count, childrenIds.Count));
      toCache.Add((id, json));
      if (toCache.Count >= CACHE_CHUNK_SIZE)
      {
        var toSave = toCache;
        toCache = new List<(string, string)>();
        tasks.Add(transport.SaveObjects(toSave));
      }
    }

    if (toCache.Count > 0)
    {
      tasks.Add(transport.SaveObjects(toCache));
    }
    await Task.WhenAll(tasks).ConfigureAwait(false);
    return (rootJson, childrenIds);
  }

  private async IAsyncEnumerable<string> CheckCache(IEnumerable<string> objectIds)
  {
    await foreach (var (id, result) in transport.HasObjects2(objectIds))
    {
      if (!result)
      {
        yield return id;
      }
    }
  }

  public void Dispose() => _api.Dispose();
}
