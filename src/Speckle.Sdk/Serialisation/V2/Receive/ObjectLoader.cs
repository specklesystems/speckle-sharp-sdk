using Speckle.InterfaceGenerator;
using Speckle.Sdk.Common;
using Speckle.Sdk.Dependencies.Serialization;
using Speckle.Sdk.Serialisation.Utilities;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Serialisation.V2.Receive;

[GenerateAutoInterface]
public sealed class ObjectLoader(
  ISQLiteReceiveCacheManager sqliteReceiveCacheManager,
  IServerObjectManager serverObjectManager,
  string streamId,
  IProgress<ProgressArgs>? progress
) : ChannelLoader, IObjectLoader
{
  private int? _allChildrenCount;
  private long _checkCache;
  private long _cached;
  private DeserializeOptions _options = new(false);

  public async Task<(string, IReadOnlyList<string>)> GetAndCache(
    string rootId,
    DeserializeOptions options,
    CancellationToken cancellationToken
  )
  {
    _options = options;
    string? rootJson;
    if (!options.SkipCache)
    {
      rootJson = sqliteReceiveCacheManager.GetObject(rootId);
      if (rootJson != null)
      {
        //assume everything exists as the root is there.
        var allChildren = ClosureParser.GetChildrenIds(rootJson).ToList();
        return (rootJson, allChildren);
      }
    }
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
    await GetAndCache(allChildrenIds, cancellationToken).ConfigureAwait(false);

    //save the root last to shortcut later
    if (!options.SkipCache)
    {
      sqliteReceiveCacheManager.SaveObject(new(rootId, rootJson));
    }
    return (rootJson, allChildrenIds);
  }

  [AutoInterfaceIgnore]
  public override string? CheckCache(string id)
  {
    _checkCache++;
    progress?.Report(new(ProgressEvent.CacheCheck, _checkCache, _allChildrenCount));
    if (!_options.SkipCache && !sqliteReceiveCacheManager.HasObject(id))
    {
      return id;
    }

    return null;
  }

  [AutoInterfaceIgnore]
  public override async Task<List<BaseItem>> DownloadAndCache(List<string?> ids)
  {
    var count = 0L;
    progress?.Report(new(ProgressEvent.DownloadObject, count, _allChildrenCount));
    var toCache = new List<BaseItem>();
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
      toCache.Add(new(id, json));
    }

    return toCache;
  }

  [AutoInterfaceIgnore]
  public override void SaveToCache(BaseItem x)
  {
    if (!_options.SkipCache)
    {
      sqliteReceiveCacheManager.SaveObject(x);
    }

    _cached++;
    progress?.Report(new(ProgressEvent.Cached, _cached, _allChildrenCount));
  }

  public string? LoadId(string id) => sqliteReceiveCacheManager.GetObject(id);
}
