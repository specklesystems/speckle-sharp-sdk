using Speckle.InterfaceGenerator;
using Speckle.Sdk.Common;
using Speckle.Sdk.Dependencies;
using Speckle.Sdk.Dependencies.Serialization;
using Speckle.Sdk.Serialisation.Utilities;
using Speckle.Sdk.Serialisation.V2.Send;
using Speckle.Sdk.SQLite;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Serialisation.V2.Receive;

public partial interface IObjectLoader : IDisposable;

[GenerateAutoInterface]
public sealed class ObjectLoader(
  ISqLiteJsonCacheManager sqLiteJsonCacheManager,
  IServerObjectManager serverObjectManager,
  IProgress<ProgressArgs>? progress,
  CancellationToken cancellationToken
#pragma warning disable CS9107 // Parameter is captured into the state of the enclosing type and its value is also passed to the base constructor. The value might be captured by the base class as well.
) : ChannelLoader<BaseItem>(cancellationToken), IObjectLoader
#pragma warning restore CS9107 // Parameter is captured into the state of the enclosing type and its value is also passed to the base constructor. The value might be captured by the base class as well.
{
  private int? _allChildrenCount;
  private long _checkCache;
  private long _cached;
  private long _downloaded;
  private long _totalToDownload;
  private DeserializeProcessOptions _options = new();

  [AutoInterfaceIgnore]
  public void Dispose() => sqLiteJsonCacheManager.Dispose();

  public async Task<(Json, IReadOnlyCollection<Id>)> GetAndCache(string rootId, DeserializeProcessOptions options)
  {
    try
    {
      _options = options;
      string? rootJson;
      if (!options.SkipCache)
      {
        rootJson = sqLiteJsonCacheManager.GetObject(rootId);
        if (rootJson != null)
        {
          //assume everything exists as the root is there.
          var allChildren = ClosureParser
            .GetClosuresSorted(rootJson, cancellationToken)
            .Select(x => new Id(x.Item1))
            .ToList();
          //this probably yields away from the Main thread to let host apps update progress
          //in any case, this fixes a Revit only issue for this situation
          await Task.Yield();
          return (new(rootJson), allChildren);
        }
      }

      if (!options.SkipServer)
      {
        rootJson = await serverObjectManager
          .DownloadSingleObject(rootId, progress, cancellationToken)
          .NotNull()
          .ConfigureAwait(false);
        IReadOnlyCollection<Id> allChildrenIds = ClosureParser
          .GetClosures(rootJson, cancellationToken)
          .OrderByDescending(x => x.Item2)
          .Select(x => new Id(x.Item1))
          .Where(x => !x.Value.StartsWith("blob", StringComparison.Ordinal))
          .Freeze();
        _allChildrenCount = allChildrenIds.Count;
        ThrowIfFailed();
        await GetAndCache(allChildrenIds.Select(x => x.Value), _options.MaxParallelism).ConfigureAwait(false);
        ThrowIfFailed();
        //save the root last to shortcut later
        if (!options.SkipCache)
        {
          sqLiteJsonCacheManager.SaveObject(rootId, rootJson);
        }
        ThrowIfFailed();
        return (new(rootJson), allChildrenIds);
      }
    }
    catch (TaskCanceledException)
    {
      ThrowIfFailed();
      throw;
    }

    throw new SpeckleException("Cannot skip server and cache. Please choose one.");
  }

  [AutoInterfaceIgnore]
  public override string? CheckCache(string id)
  {
    _checkCache++;
    progress?.Report(new(ProgressEvent.CacheCheck, _checkCache, _allChildrenCount));
    if (!_options.SkipCache && !sqLiteJsonCacheManager.HasObject(id))
    {
      Interlocked.Increment(ref _totalToDownload);
      return id;
    }

    return null;
  }

  [AutoInterfaceIgnore]
  protected override async Task<List<BaseItem>> DownloadInternal(List<string?> ids)
  {
    var toCache = new List<BaseItem>();
    await foreach (
      var (id, json) in serverObjectManager.DownloadObjects(
        ids.Select(x => x.NotNull()).ToList(),
        progress,
        cancellationToken
      )
    )
    {
      cancellationToken.ThrowIfCancellationRequested();
      Interlocked.Increment(ref _downloaded);
      progress?.Report(new(ProgressEvent.DownloadObjects, _downloaded, _totalToDownload));
      toCache.Add(new(new(id), new(json), true, null));
    }

    if (toCache.Count != ids.Count)
    {
      throw new SpeckleException(
        $"Objects in batch missing: {string.Join(",", ids.Except(toCache.Select(y => y.Id.Value)).Take(10))}"
      );
    }
    return toCache;
  }

  [AutoInterfaceIgnore]
  protected override void SaveToCacheInternal(List<BaseItem> batch)
  {
    if (!_options.SkipCache)
    {
      cancellationToken.ThrowIfCancellationRequested();
      sqLiteJsonCacheManager.SaveObjects(batch.Select(x => (x.Id.Value, x.Json.Value)));
      Interlocked.Exchange(ref _cached, _cached + batch.Count);
      progress?.Report(new(ProgressEvent.CachedToLocal, _cached, _allChildrenCount));
    }
  }

  public string? LoadId(string id) => sqLiteJsonCacheManager.GetObject(id);

  private void ThrowIfFailed()
  {
    //always check for cancellation first
    cancellationToken.ThrowIfCancellationRequested();
    if (Exception is not null)
    {
      throw new SpeckleException("Error while loading", Exception);
    }
  }
}
