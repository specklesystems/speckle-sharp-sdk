using System.Collections.Concurrent;
using System.Text;
using Speckle.InterfaceGenerator;
using Speckle.Sdk.Common;
using Speckle.Sdk.Dependencies;
using Speckle.Sdk.Dependencies.Serialization;
using Speckle.Sdk.Models;
using Speckle.Sdk.SQLite;
using Speckle.Sdk.Transports;
using Closures = System.Collections.Generic.Dictionary<Speckle.Sdk.Serialisation.Id, int>;

namespace Speckle.Sdk.Serialisation.V2.Send;

public record SerializeProcessOptions(
  bool SkipCacheRead = false,
  bool SkipCacheWrite = false,
  bool SkipServer = false,
  bool SkipFindTotalObjects = false,
  bool EnableServerSending = true,
  bool EnableCacheSaving = true
);

public readonly record struct SerializeProcessResults(
  string RootId,
  IReadOnlyDictionary<Id, ObjectReference> ConvertedReferences
);

public readonly record struct BaseItem(Id Id, Json Json, bool NeedsStorage, Closures? Closures) : IHasSize
{
  public int Size { get; } = Encoding.UTF8.GetByteCount(Json.Value);

  public bool Equals(BaseItem? other)
  {
    if (other is null)
    {
      return false;
    }
    return string.Equals(Id.Value, other.Value.Id.Value, StringComparison.OrdinalIgnoreCase);
  }

  public override int GetHashCode() => Id.GetHashCode();
}

[GenerateAutoInterface]
public class SerializeProcess(
  IProgress<ProgressArgs>? progress,
  ISqLiteJsonCacheManager sqLiteJsonCacheManager,
  IServerObjectManager serverObjectManager,
  IBaseChildFinder baseChildFinder,
  IObjectSerializerFactory objectSerializerFactory,
  SerializeProcessOptions? options = null
) : ChannelSaver<BaseItem>, ISerializeProcess
{
  private readonly PriorityScheduler _countScheduler = new PriorityScheduler(ThreadPriority.Highest);
  private readonly PriorityScheduler _writeScheduler = new PriorityScheduler(ThreadPriority.Normal);
  
  
  private readonly SerializeProcessOptions _options = options ?? new(false, false, false, false);

  private readonly ConcurrentDictionary<Id, ObjectReference> _objectReferences = new();
  private readonly Pool<List<(Id, Json, Closures)>> _pool = Pools.CreateListPool<(Id, Json, Closures)>();
  private readonly Pool<Dictionary<Id, NodeInfo>> _childClosurePool = Pools.CreateDictionaryPool<Id, NodeInfo>();

  private long _objectCount;
  private long _objectsFound;

  private long _objectsSerialized;

  private long _uploaded;
  private long _cached;

  [AutoInterfaceIgnore]
  public void Dispose()
  {
    sqLiteJsonCacheManager.Dispose();
    _countScheduler.Dispose();
    _writeScheduler.Dispose();
  }

  public async Task<SerializeProcessResults> Serialize(Base root, CancellationToken cancellationToken)
  {
    var channelTask = Start(_options.EnableServerSending, _options.EnableCacheSaving, cancellationToken);
    var findTotalObjectsTask = Task.CompletedTask;
    if (!_options.SkipFindTotalObjects)
    {
      findTotalObjectsTask = Task.Factory.StartNew(
        () => TraverseTotal(root),
        default,
        TaskCreationOptions.AttachedToParent,
        _countScheduler
      );
    }

    await Traverse(root, true, cancellationToken).ConfigureAwait(false);
    await channelTask.ConfigureAwait(false);
    await findTotalObjectsTask.ConfigureAwait(false);
    return new(root.id.NotNull(), _objectReferences.Freeze());
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

  private async Task<Dictionary<Id, NodeInfo>> Traverse(Base obj, bool isEnd, CancellationToken cancellationToken)
  {
    var tasks = new List<Task<Dictionary<Id, NodeInfo>>>();
    foreach (var child in baseChildFinder.GetChildren(obj))
    {
      // tmp is necessary because of the way closures close over loop variables
      var tmp = child;
      var t = Task
        .Factory.StartNew(
          () => Traverse(tmp, false, cancellationToken),
          cancellationToken,
          TaskCreationOptions.AttachedToParent,
          _writeScheduler
        )
        .Unwrap();
      tasks.Add(t);
    }

    if (tasks.Count > 0)
    {
      await Task.WhenAll(tasks).ConfigureAwait(false);
    }
    var childClosures = _childClosurePool.Get();
    foreach (var t in tasks)
    {
      var childClosure = t.Result;
      foreach (var kvp in childClosure)
      {
        childClosures[kvp.Key] = kvp.Value;
      }
    }

    var items = Serialise(obj, childClosures, cancellationToken);

    var currentClosures = new Dictionary<Id, NodeInfo>();
    Interlocked.Increment(ref _objectCount);
    progress?.Report(new(ProgressEvent.FromCacheOrSerialized, _objectCount, _objectsFound));
    foreach (var item in items)
    {
      if (item.NeedsStorage)
      {
        var saving = Task.Factory.StartNew(async () =>
            await Save(item, cancellationToken).ConfigureAwait(false),
          cancellationToken, TaskCreationOptions.AttachedToParent, _writeScheduler);
        await saving.Unwrap().ConfigureAwait(false);
      }

      if (!currentClosures.ContainsKey(item.Id))
      {
        currentClosures.Add(item.Id, new NodeInfo(item.Json, item.Closures));
      }
    }
    _childClosurePool.Return(childClosures);

    if (isEnd)
    {
      await Done().ConfigureAwait(false);
    }

    return currentClosures;
  }

  //leave this sync
  private IEnumerable<BaseItem> Serialise(
    Base obj,
    IReadOnlyDictionary<Id, NodeInfo> childInfo,
    CancellationToken cancellationToken
  )
  {
    if (!_options.SkipCacheRead && obj.id != null)
    {
      var cachedJson = sqLiteJsonCacheManager.GetObject(obj.id);
      if (cachedJson != null)
      {
        yield return new BaseItem(new(obj.id.NotNull()), new(cachedJson), false, null);
        yield break;
      }
    }

    using var serializer2 = objectSerializerFactory.Create(childInfo, cancellationToken);
    var items = _pool.Get();
    try
    {
      items.AddRange(serializer2.Serialize(obj));
      Interlocked.Add(ref _objectsSerialized, items.Count);
      foreach (var kvp in serializer2.ObjectReferences)
      {
        _objectReferences.TryAdd(kvp.Key, kvp.Value);
      }

      var (id, json, closures) = items.First();
      yield return CheckCache(id, json, closures);
      foreach (var (cid, cJson, cClosures) in items.Skip(1))
      {
        yield return CheckCache(cid, cJson, cClosures);
      }
    }
    finally
    {
      _pool.Return(items);
    }
  }

  private BaseItem CheckCache(Id id, Json json, Dictionary<Id, int> closures)
  {
    if (!_options.SkipCacheRead)
    {
      var cachedJson = sqLiteJsonCacheManager.GetObject(id.Value);
      if (cachedJson != null)
      {
        return new BaseItem(id, new(cachedJson), false, null);
      }
    }
    return new BaseItem(id, json, true, closures);
  }

  public override async Task<List<BaseItem>> SendToServer(List<BaseItem> batch, CancellationToken cancellationToken)
  {
    if (!_options.SkipServer && batch.Count != 0)
    {
      var objectBatch = batch.Distinct().ToList();
      var hasObjects = await serverObjectManager
        .HasObjects(objectBatch.Select(x => x.Id.Value).Freeze(), cancellationToken)
        .ConfigureAwait(false);
      objectBatch = batch.Where(x => !hasObjects[x.Id.Value]).ToList();
      if (objectBatch.Count != 0)
      {
        await serverObjectManager.UploadObjects(objectBatch, true, progress, cancellationToken).ConfigureAwait(false);
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
      sqLiteJsonCacheManager.SaveObjects(batch.Select(x => (x.Id.Value, x.Json.Value)));
      Interlocked.Exchange(ref _cached, _cached + batch.Count);
      progress?.Report(new(ProgressEvent.CachedToLocal, _cached, _objectsSerialized));
    }
  }
}
