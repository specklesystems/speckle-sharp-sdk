using System.Collections.Concurrent;
using Speckle.InterfaceGenerator;
using Speckle.Sdk.Common;
using Speckle.Sdk.Helpers;
using Speckle.Sdk.Logging;
using Speckle.Sdk.Serialisation.Utilities;
using Speckle.Sdk.Transports;
using Speckle.Sdk.Transports.ServerUtils;

namespace Speckle.Sdk.Serialisation.Receive;

[GenerateAutoInterface]
public sealed class ObjectLoader(
  ISpeckleHttp http,
  ISdkActivityFactory activityFactory,
  Uri serverUrl,
  string streamId,
  string? token,
  IProgress<ProgressArgs>? progress,
  SQLiteTransport transport
) : IObjectLoader, IDisposable
{
  private const int HTTP_ID_CHUNK_SIZE = 500;
  private const int CACHE_CHUNK_SIZE = 500;

  private const int MAX_PARALLELISM_HTTP = 4;

  //private static readonly int MAX_PARALLELISM_CACHE = Environment.ProcessorCount * 2;

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

  public async Task<(string, IReadOnlyList<string>)> GetAndCache(string rootId, CancellationToken cancellationToken)
  {
    var rootJson = await GetRootJson(rootId).ConfigureAwait(false);
    var allChildrenIds = ClosureParser
      .GetClosures(rootJson)
      .OrderByDescending(x => x.Item2)
      .Select(x => x.Item1)
      .Where(x => !x.StartsWith("blob", StringComparison.Ordinal))
      .ToList();
    var idsToDownload = CheckCache(allChildrenIds);
    await DownloadAndCache(idsToDownload, cancellationToken).ConfigureAwait(false);
    return (rootJson, allChildrenIds);
  }

  private async IAsyncEnumerable<string> CheckCache(IReadOnlyList<string> childrenIds)
  {
    var count = 0L;
    progress?.Report(new(ProgressEvent.CacheCheck, count, childrenIds.Count));
    await foreach (
      var (id, result) in childrenIds.Batch(CACHE_CHUNK_SIZE).Select(x => transport.HasObjects2(x)).SelectManyAsync()
    )
    {
      count++;
      progress?.Report(new(ProgressEvent.CacheCheck, count, childrenIds.Count));
      if (!result)
      {
        yield return id;
      }
    }
  }

  private async Task DownloadAndCache(IAsyncEnumerable<string> ids, CancellationToken cancellationToken)
  {
    var count = 0L;
    progress?.Report(new(ProgressEvent.DownloadObject, count, null));
    var toCache = new List<(string, string)>();
    var tasks = new ConcurrentBag<Task>();
    using SemaphoreSlim ss = new(MAX_PARALLELISM_HTTP, MAX_PARALLELISM_HTTP);
    await foreach (var idBatch in ids.BatchAsync(HTTP_ID_CHUNK_SIZE).WithCancellation(cancellationToken))
    {
      await ss.WaitAsync(cancellationToken).ConfigureAwait(false);
      try
      {
        await foreach (
          var (id, json) in _api.DownloadObjectsImpl2(streamId, idBatch, progress).WithCancellation(cancellationToken)
        )
        {
          count++;
          progress?.Report(new(ProgressEvent.DownloadObject, count, null));
          toCache.Add((id, json));
          if (toCache.Count >= CACHE_CHUNK_SIZE)
          {
            var toSave = toCache;
            toCache = new List<(string, string)>();
            tasks.Add(transport.SaveObjects(toSave));
          }
        }
      }
      finally
      {
        ss.Release();
      }
    }

    if (toCache.Count > 0)
    {
      tasks.Add(transport.SaveObjects(toCache));
    }

    await Task.WhenAll(tasks).ConfigureAwait(false);
  }

  public IEnumerable<(string, string)> LoadIds(IReadOnlyList<string> ids) => transport.GetObjects(ids);

  public void Dispose() => _api.Dispose();
}
