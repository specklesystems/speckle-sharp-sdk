using Open.ChannelExtensions;
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
  private const int MAX_PARALLELISM_HTTP = 4;

  private int? _allChildrenCount;
  private long _checkCache;
  private long _cached;

  public async Task<(string, IReadOnlyList<string>)> GetAndCache(
    string rootId,
    CancellationToken cancellationToken,
    DeserializeOptions? options = null
  )
  {
    var rootJson = sqLiteCacheManager.GetObject(rootId);
    /*if (rootJson != null)
    {
      //assume everything exists as the root is there.
      var allChildren = ClosureParser.GetChildrenIds(rootJson).ToList();
      return (rootJson, allChildren);
    }*/
    rootJson = await serverObjectManager
      .DownloadSingleObject(streamId, rootId, progress, cancellationToken)
      .NotNull()
      .ConfigureAwait(false);
    List<string> allChildrenIds = ClosureParser
      .GetClosures(rootJson)
      .OrderByDescending(x => x.Item2)
      .Select(x => x.Item1)
      .Where(x => !x.StartsWith("blob", StringComparison.Ordinal))
      .ToList();
    _allChildrenCount = allChildrenIds.Count;
    await allChildrenIds
      .ToChannel()
      .Pipe(Environment.ProcessorCount, CheckCache2)
      .Filter(x => x is not null)
      .Batch(HTTP_ID_CHUNK_SIZE)
      .WithTimeout(TimeSpan.FromSeconds(2))
      .PipeAsync(MAX_PARALLELISM_HTTP, DownloadAndCache2, -1, false, cancellationToken)
      .Join()
      .ReadAllConcurrently(Environment.ProcessorCount, SaveToCache, cancellationToken: cancellationToken)
      .ConfigureAwait(false);

    //save the root last to shortcut later
    sqLiteCacheManager.SaveObjectSync(rootId, rootJson);
    return (rootJson, allChildrenIds);
  }

  private string? CheckCache2(string id)
  {
    _checkCache++;
    progress?.Report(new(ProgressEvent.CacheCheck, _checkCache, _allChildrenCount));
    if (!sqLiteCacheManager.HasObject(id))
    {
      return id;
    }

    return null;
  }

  private async ValueTask<List<(string, string)>> DownloadAndCache2(List<string?> ids)
  {
    var count = 0L;
    progress?.Report(new(ProgressEvent.DownloadObject, count, _allChildrenCount));
    var toCache = new List<(string, string)>();
    await foreach (
      var (id, json) in serverObjectManager.DownloadObjects(
        streamId,
        ids.Select(x => x.NotNull()).ToList(),
        progress,
        default
      )
    )
    {
      count++;
      progress?.Report(new(ProgressEvent.DownloadObject, count, _allChildrenCount));
      toCache.Add((id, json));
    }

    return toCache;
  }

  private void SaveToCache((string, string) x)
  {
    sqLiteCacheManager.SaveObjectSync(x.Item1, x.Item2);

    _cached++;
    progress?.Report(new(ProgressEvent.Cached, _cached, null));
  }

  public string? LoadId(string id) => sqLiteCacheManager.GetObject(id);
}
