using System.Collections.Concurrent;
using CodeJam.Threading;
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
  private const int HTTP_ID_CHUNK_SIZE = 50;
  private const int CACHE_CHUNK_SIZE = 500;
  private const int MAX_PARALLELISM_HTTP = 4;
  private static readonly int MAX_PARALLELISM_CACHE = Environment.ProcessorCount * 2;
  
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
    var childrenIds = ClosureParser.GetClosures(rootJson).OrderByDescending(x => x.Item2).Select(x => x.Item1);
    var allChildrenIds = childrenIds.Where(x => !x.StartsWith("blob", StringComparison.Ordinal)).ToList();
    var idsToDownload = await CheckCache(allChildrenIds, cancellationToken).ConfigureAwait(false);
    await DownloadAndCache(idsToDownload, cancellationToken).ConfigureAwait(false);
    return (rootJson, allChildrenIds);
  }

  private async Task<IReadOnlyCollection<string>> CheckCache(IReadOnlyList<string> childrenIds, CancellationToken cancellationToken)
  {
    var count = 0L;
    progress?.Report(new(ProgressEvent.CacheCheck, count, childrenIds.Count));
    ConcurrentBag<string> nonCachedChildIds = new();
    await childrenIds
      .Batch(CACHE_CHUNK_SIZE)
      .ForEachAsync(
        async (batch, ct) =>
        {
          await foreach (var (id, result) in transport.HasObjects2(batch).WithCancellation(ct))
          {
            count++;
            progress?.Report(new(ProgressEvent.CacheCheck, count, childrenIds.Count));
            if (!result)
            {
              nonCachedChildIds.Add(id);
            }
          }
        },
        MAX_PARALLELISM_CACHE,
        cancellationToken
      )
      .ConfigureAwait(false);
    return nonCachedChildIds;
  }

  private async Task DownloadAndCache(IReadOnlyCollection<string> ids, CancellationToken cancellationToken)
  {
    var count = 0L;
    progress?.Report(new(ProgressEvent.DownloadObject, count, ids.Count));
    var toCache = new List<(string, string)>();
    var tasks = new ConcurrentBag<Task>();
    await ids
      .Batch(HTTP_ID_CHUNK_SIZE)
      .ForEachAsync(
        async (batch, ct) =>
        {
          await foreach (
            var (id, json) in _api.DownloadObjectsImpl2(streamId, batch, progress).WithCancellation(ct)
          )
          {
            count++;
            progress?.Report(new(ProgressEvent.DownloadObject, count, ids.Count));
            toCache.Add((id, json));
            if (toCache.Count >= CACHE_CHUNK_SIZE)
            {
              var toSave = toCache;
              toCache = new List<(string, string)>();
              tasks.Add(transport.SaveObjects(toSave));
            }
          }
        },
        MAX_PARALLELISM_HTTP,
        cancellationToken
      )
      .ConfigureAwait(false);

    if (toCache.Count > 0)
    {
      tasks.Add(transport.SaveObjects(toCache));
    }

    await Task.WhenAll(tasks).ConfigureAwait(false);
  }

  public IEnumerable<(string, string)> LoadIds(IReadOnlyList<string> ids) => transport.GetObjects(ids);
  public void Dispose() => _api.Dispose();
}
