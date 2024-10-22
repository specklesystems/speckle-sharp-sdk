using System.Collections.Concurrent;
using Speckle.InterfaceGenerator;
using Speckle.Sdk.Common;
using Speckle.Sdk.Serialisation.Utilities;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Serialisation.V2.Receive;

[GenerateAutoInterface]
public sealed class ObjectLoader(
  ISQLiteCacheManager sqLiteCacheManager,
  IServerObjectManager serverObjectManager,
  string streamId,
  IProgress<ProgressArgs>? progress
) : IObjectLoader
{
  private const int HTTP_ID_CHUNK_SIZE = 500;
  private const int CACHE_CHUNK_SIZE = 3000;
  private const int MAX_PARALLELISM_HTTP = 4;

  public async Task<(string, IReadOnlyList<string>)> GetAndCache(
    string rootId,
    CancellationToken cancellationToken,
    DeserializeOptions? options = null
  )
  {
    var rootJson = sqLiteCacheManager.GetObject(rootId);
    if (rootJson != null)
    {
      //assume everything exists as the root is there.
      var allChildren = ClosureParser.GetChildrenIds(rootJson).ToList();
      return (rootJson, allChildren);
    }
    rootJson = await serverObjectManager
      .DownloadSingleObject(streamId, rootId, progress, cancellationToken)
      .NotNull()
      .ConfigureAwait(false);
    var allChildrenIds = ClosureParser
      .GetClosures(rootJson)
      .OrderByDescending(x => x.Item2)
      .Select(x => x.Item1)
      .Where(x => !x.StartsWith("blob", StringComparison.Ordinal))
      .ToList();
    if (!(options?.SkipCacheCheck ?? false))
    {
      var idsToDownload = CheckCache(allChildrenIds);
      await DownloadAndCache(idsToDownload, cancellationToken).ConfigureAwait(false);
    }
    //save the root last to shortcut later
    sqLiteCacheManager.SaveObjectSync(rootId, rootJson);
    return (rootJson, allChildrenIds);
  }

  private async IAsyncEnumerable<string> CheckCache(IReadOnlyList<string> childrenIds)
  {
    var count = 0L;
    progress?.Report(new(ProgressEvent.CacheCheck, count, childrenIds.Count));
    await foreach (
      var (id, result) in childrenIds
        .Batch(CACHE_CHUNK_SIZE)
        .Select(x => sqLiteCacheManager.HasObjects2(x)) // there needs to be a Task somewhere here
        .SelectManyAsync()
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
          var (id, json) in serverObjectManager.DownloadObjects(streamId, idBatch, progress, cancellationToken)
        )
        {
          count++;
          progress?.Report(new(ProgressEvent.DownloadObject, count, null));
          toCache.Add((id, json));
          if (toCache.Count >= CACHE_CHUNK_SIZE)
          {
            var toSave = toCache;
            toCache = new List<(string, string)>();
#pragma warning disable CA2008
            tasks.Add(
              Task.Factory.StartNew(() => sqLiteCacheManager.SaveObjects(toSave, cancellationToken), cancellationToken)
            );
#pragma warning restore CA2008
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
#pragma warning disable CA2008
      tasks.Add(
        Task.Factory.StartNew(() => sqLiteCacheManager.SaveObjects(toCache, cancellationToken), cancellationToken)
      );
#pragma warning restore CA2008
    }

    await Task.WhenAll(tasks).ConfigureAwait(false);
  }

  public string? LoadId(string id) => sqLiteCacheManager.GetObject(id);
}
