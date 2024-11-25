using Speckle.InterfaceGenerator;
using Speckle.Sdk.Common;
using Speckle.Sdk.Dependencies;
using Speckle.Sdk.Dependencies.Serialization;
using Speckle.Sdk.Serialisation.Utilities;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Serialisation.V2.Receive;

[GenerateAutoInterface]
public sealed class ObjectLoader(
  ISQLiteReceiveCacheManager sqliteReceiveCacheManager,
  IServerObjectManager serverObjectManager,
  IProgress<ProgressArgs>? progress
) : ChannelLoader, IObjectLoader
{
  private int? _allChildrenCount;
  private long _checkCache;
  private long _cached;
  private DeserializeOptions _options = new(false);

  public async Task<(string, IReadOnlyCollection<string>)> GetAndCache(
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
      .DownloadSingleObject(rootId, progress, cancellationToken)
      .NotNull()
      .ConfigureAwait(false);
    IReadOnlyCollection<string> allChildrenIds = ClosureParser
      .GetClosures(rootJson)
      .OrderByDescending(x => x.Item2)
      .Select(x => x.Item1)
      .Where(x => !x.StartsWith("blob", StringComparison.Ordinal))
      .Freeze();
    _allChildrenCount = allChildrenIds.Count;
    await GetAndCache(allChildrenIds, cancellationToken).ConfigureAwait(false);

    //save the root last to shortcut later
    if (!options.SkipCache)
    {
      sqliteReceiveCacheManager.SaveObject(new(rootId, rootJson, true));
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
  public override async Task<List<BaseItem>> Download(List<string?> ids)
  {
    var toCache = new List<BaseItem>();
    await foreach (
      var (id, json) in serverObjectManager.DownloadObjects(ids.Select(x => x.NotNull()).ToList(), progress, default)
    )
    {
      toCache.Add(new(id, json, true));
    }

    if (toCache.Count != ids.Count)
    {
      throw new SpeckleException(
        $"Objects in batch missing: {string.Join(",", ids.Except(toCache.Select(y => y.Id)).Take(10))}"
      );
    }
    return toCache;
  }

  [AutoInterfaceIgnore]
  public override void SaveToCache(List<BaseItem> batch)
  {
    if (!_options.SkipCache)
    {
      sqliteReceiveCacheManager.SaveObjects(batch);
      Interlocked.Exchange(ref _cached, _cached + batch.Count);
      progress?.Report(new(ProgressEvent.CachedToLocal, _cached, _allChildrenCount));
    }
  }

  public string? LoadId(string id) => sqliteReceiveCacheManager.GetObject(id);
}
