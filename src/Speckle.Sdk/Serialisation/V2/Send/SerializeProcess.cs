using System.Collections.Concurrent;
using Speckle.InterfaceGenerator;
using Speckle.Sdk.Common;
using Speckle.Sdk.Dependencies;
using Speckle.Sdk.Dependencies.Serialization;
using Speckle.Sdk.Models;
using Speckle.Sdk.SQLite;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Serialisation.V2.Send;

public record SerializeProcessOptions(
  bool SkipCacheRead,
  bool SkipCacheWrite,
  bool SkipServer,
  bool SkipFindTotalObjects
);

public readonly record struct SerializeProcessResults(
  string RootId,
  IReadOnlyDictionary<Id, ObjectReference> ConvertedReferences
);

[GenerateAutoInterface]
public class SerializeProcess(
  IProgress<ProgressArgs>? progress,
  ISqLiteJsonCacheManager sqLiteJsonCacheManager,
  IServerObjectManager serverObjectManager,
  IBaseChildFinder baseChildFinder,
  IObjectSerializerFactory objectSerializerFactory,
  SerializeProcessOptions? options = null
) : ChannelSaver, ISerializeProcess
{
  private readonly SerializeProcessOptions _options = options ?? new(false, false, false, false);

  //cache bases and closure info to avoid reserialization
  private readonly IDictionary<Base, CacheInfo> _baseCache = new ConcurrentDictionary<Base, CacheInfo>();
  private readonly ConcurrentDictionary<Id, ObjectReference> _objectReferences = new();
  private readonly Pool<List<(Id, Json)>> _pool = Pools.CreateListPool<(Id, Json)>();

  private long _objectCount;
  private long _objectsFound;

  private long _objectsSerialized;

  private long _uploaded;
  private long _cached;

  public async Task<SerializeProcessResults> Serialize(Base root, CancellationToken cancellationToken)
  {
    var channelTask = Start(!_options.SkipServer, !_options.SkipCacheWrite, cancellationToken);
    var findTotalObjectsTask = Task.CompletedTask;
    if (!_options.SkipFindTotalObjects)
    {
      findTotalObjectsTask = Task.Factory.StartNew(
        () => TraverseTotal(root),
        default,
        TaskCreationOptions.LongRunning,
        TaskScheduler.Default
      );
    }

    await Traverse(root, true, cancellationToken).ConfigureAwait(false);
    await channelTask.ConfigureAwait(false);
    await findTotalObjectsTask.ConfigureAwait(false);
    return new(root.id.NotNull(), _objectReferences);
  }

  private void TraverseTotal(Base obj)
  {
    foreach (var child in baseChildFinder.GetChildren(obj))
    {
      _objectsFound++;
      progress?.Report(new(ProgressEvent.FindingChildren, _objectsFound, null));
      TraverseTotal(child);
    }
  }

  private async Task Traverse(Base obj, bool isEnd, CancellationToken cancellationToken)
  {
    var tasks = new List<Task>();
    foreach (var child in baseChildFinder.GetChildren(obj))
    {
      // tmp is necessary because of the way closures close over loop variables
      var tmp = child;
      var t = Task
        .Factory.StartNew(
          () => Traverse(tmp, false, cancellationToken),
          cancellationToken,
          TaskCreationOptions.AttachedToParent,
          TaskScheduler.Default
        )
        .Unwrap();
      tasks.Add(t);
    }

    if (tasks.Count > 0)
    {
      await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    var items = Serialise(obj, cancellationToken);
    Interlocked.Increment(ref _objectCount);
    progress?.Report(new(ProgressEvent.FromCacheOrSerialized, _objectCount, _objectsFound));
    foreach (var item in items)
    {
      if (item.NeedsStorage)
      {
        await Save(item, cancellationToken).ConfigureAwait(false);
      }
    }

    if (isEnd)
    {
      await Done().ConfigureAwait(false);
    }
  }

  //leave this sync
  private IEnumerable<BaseItem> Serialise(Base obj, CancellationToken cancellationToken)
  {
    if (!_options.SkipCacheRead && obj.id != null)
    {
      var cachedJson = sqLiteJsonCacheManager.GetObject(obj.id);
      if (cachedJson != null)
      {
        yield return new BaseItem(obj.id.NotNull(), cachedJson, false);
        yield break;
      }
    }

    var serializer2 = objectSerializerFactory.Create(_baseCache, cancellationToken);
    var items = _pool.Get();
    try
    {
      items.AddRange(serializer2.Serialize(obj));
      Interlocked.Add(ref _objectsSerialized, items.Count);
      foreach (var kvp in serializer2.ObjectReferences)
      {
        _objectReferences.TryAdd(kvp.Key, kvp.Value);
      }

      var (id, json) = items.First();
      yield return CheckCache(id, json);
      foreach (var (cid, cJson) in items.Skip(1))
      {
        yield return CheckCache(cid, cJson);
      }
    }
    finally
    {
      _pool.Return(items);
    }
  }

  private BaseItem CheckCache(Id id, Json json)
  {
    if (!_options.SkipCacheRead)
    {
      var cachedJson = sqLiteJsonCacheManager.GetObject(id.Value);
      if (cachedJson != null)
      {
        return new BaseItem(id.Value, cachedJson, false);
      }
    }
    return new BaseItem(id.Value, json.Value, true);
  }

  public override async Task<List<BaseItem>> SendToServer(List<BaseItem> batch, CancellationToken cancellationToken)
  {
    if (!_options.SkipServer && batch.Count != 0)
    {
      var objectBatchIds = batch.Select(x => x.Id).Distinct().Freeze();
      var hasObjects = await serverObjectManager
        .HasObjects(objectBatchIds, cancellationToken)
        .ConfigureAwait(false);
      objectBatchIds = objectBatchIds.Where(x => !hasObjects[x]).Freeze();
      if (objectBatchIds.Count != 0)
      {
        await serverObjectManager.UploadObjects(batch.Where(x => objectBatchIds.Contains(x.Id)).ToList(), true, progress, cancellationToken).ConfigureAwait(false);
        Interlocked.Exchange(ref _uploaded, _uploaded + batch.Count);
      }

      progress?.Report(new(ProgressEvent.UploadedObjects, _uploaded, null));
    }
    return batch;
  }

  public override void SaveToCache(List<BaseItem> batch)
  {
    if (!_options.SkipCacheWrite && batch.Count != 0)
    {
      sqLiteJsonCacheManager.SaveObjects(batch.Select(x => (x.Id, x.Json)));
      Interlocked.Exchange(ref _cached, _cached + batch.Count);
      progress?.Report(new(ProgressEvent.CachedToLocal, _cached, _objectsSerialized));
    }
  }
}
