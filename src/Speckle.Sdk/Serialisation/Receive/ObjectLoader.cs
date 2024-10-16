using Speckle.Sdk.Common;
using Speckle.Sdk.Helpers;
using Speckle.Sdk.Logging;
using Speckle.Sdk.Serialisation.Utilities;
using Speckle.Sdk.Transports;
using Speckle.Sdk.Transports.ServerUtils;

namespace Speckle.Sdk.Serialisation.Receive;

public sealed class ObjectLoader(ISpeckleHttp http,
  ISdkActivityFactory activityFactory,
  Uri serverUrl,
  string streamId,
  string? token,
   IProgress<ProgressArgs>? progress,
     SQLiteTransport transport) : IDisposable
{
  private const int HTTP_ID_CHUNK_SIZE = 50;
  private const int CACHE_CHUNK_SIZE = 50;
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

  public async Task DownloadAndCache(string rootId, CancellationToken cancellationToken)
  {
    var rootJson = await GetRootJson(rootId).ConfigureAwait(false);
    var nonCachedChildIds = ClosureParser.GetChildrenIds(rootJson)
      .Where(x => !x.StartsWith("blob", StringComparison.Ordinal))
      .Batch(CACHE_CHUNK_SIZE) //review this after putting in sqlite cache
      .Select(x => CheckCache(x))
      .SelectManyAsync()
      .BatchAsync(HTTP_ID_CHUNK_SIZE);
    
    var downloads = new List<IAsyncEnumerable<(string, string)>>();
    await foreach (var batch in nonCachedChildIds)
    {
      downloads.Add(_api.DownloadObjectsImpl2(streamId, batch, progress));
    }

    var toCache = new List<(string, string)>();
    var tasks = new List<Task>();
    await foreach (var (id, json) in downloads.SelectManyAsync().WithCancellation(cancellationToken))
    {
      toCache.Add((id, json));
      if (toCache.Count >= CACHE_CHUNK_SIZE)
      {
        Console.WriteLine("Caching objects " + toCache.Count);
        var toSave = toCache;
        toCache = new List<(string, string)>();
        tasks.Add(transport.SaveObjects(toSave));
      }
    }

    if (toCache.Count > 0)
    {
      tasks.Add(transport.SaveObjects(toCache));
      Console.WriteLine("Final cache " + toCache.Count);
    }
    await Task.WhenAll(tasks).ConfigureAwait(false);
  }

  private async IAsyncEnumerable<string> CheckCache(IEnumerable<string> objectIds)
  {
    Console.WriteLine("Checking cache objects");
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
