using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
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
  bool SkipFindTotalObjects = false
);

public readonly record struct SerializeProcessResults(
  string RootId,
  IReadOnlyDictionary<Id, ObjectReference> ConvertedReferences
);

public partial interface ISerializeProcess : IDisposable;

[GenerateAutoInterface]
public sealed class SerializeProcess(
  IProgress<ProgressArgs>? progress,
  ISqLiteJsonCacheManager sqLiteJsonCacheManager,
  IServerObjectManager serverObjectManager,
  IBaseChildFinder baseChildFinder,
  IObjectSerializerFactory objectSerializerFactory,
  ILoggerFactory loggerFactory,
  SerializeProcessOptions? options = null
) : ChannelSaver<BaseItem>, ISerializeProcess
{
  private readonly PriorityScheduler _highest = new(
    loggerFactory.CreateLogger<PriorityScheduler>(),
    ThreadPriority.Highest,
    2
  );
  private readonly PriorityScheduler _belowNormal = new(
    loggerFactory.CreateLogger<PriorityScheduler>(),
    ThreadPriority.BelowNormal,
    Environment.ProcessorCount * 2
  );

  private readonly SerializeProcessOptions _options = options ?? new(false, false, false, false);
  private readonly ILogger<SerializeProcess> _logger = loggerFactory.CreateLogger<SerializeProcess>();

  private readonly ConcurrentDictionary<Id, ObjectReference> _objectReferences = new();
  private readonly Pool<List<(Id, Json, Closures)>> _pool = Pools.CreateListPool<(Id, Json, Closures)>();
  private readonly Pool<Dictionary<Id, NodeInfo>> _currentClosurePool = Pools.CreateDictionaryPool<Id, NodeInfo>();
  private readonly Pool<ConcurrentDictionary<Id, NodeInfo>> _childClosurePool = Pools.CreateConcurrentDictionaryPool<
    Id,
    NodeInfo
  >();

  private long _objectCount;
  private long _objectsFound;

  private long _objectsSerialized;

  private long _uploaded;
  private long _cached;

  [AutoInterfaceIgnore]
  public void Dispose()
  {
    _highest.Dispose();
    _belowNormal.Dispose();
    sqLiteJsonCacheManager.Dispose();
  }

  public async Task<SerializeProcessResults> Serialize(Base root, CancellationToken cancellationToken)
  {
    using var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    var channelTask = Start(source);
    var findTotalObjectsTask = Task.CompletedTask;
    if (!_options.SkipFindTotalObjects)
    {
      findTotalObjectsTask = Task.Factory.StartNew(
        () => TraverseTotal(root),
        source.Token,
        TaskCreationOptions.AttachedToParent | TaskCreationOptions.PreferFairness,
        _highest
      );
    }

    await Traverse(root, source).ConfigureAwait(false);
    await Done().ConfigureAwait(true);
    await channelTask.ConfigureAwait(false);
    await findTotalObjectsTask.ConfigureAwait(false);
    if (source.IsCancellationRequested)
    {
      //this means we really want to stop but exceptions haven't bubbled up yet so don't let us continue if cancelled
      throw new OperationCanceledException();
    }
    //last chance to see if user cancelled
    cancellationToken.ThrowIfCancellationRequested();
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

  private async Task<Dictionary<Id, NodeInfo>> Traverse(Base obj, CancellationTokenSource cancellationTokenSource)
  {
    var tasks = new List<Task<Dictionary<Id, NodeInfo>>>();
    foreach (var child in baseChildFinder.GetChildren(obj))
    {
      if (cancellationTokenSource.IsCancellationRequested)
      {
        return [];
      }
      // tmp is necessary because of the way closures close over loop variables
      var tmp = child;
      var t = Task
        .Factory.StartNew(
          async () =>
          {
            try
            {
              return await Traverse(tmp, cancellationTokenSource).ConfigureAwait(true);
            }
            catch (Exception)
            {
              cancellationTokenSource.Cancel(true);
              throw;
            }
          },
          cancellationTokenSource.Token,
          TaskCreationOptions.AttachedToParent | TaskCreationOptions.PreferFairness,
          _belowNormal
        )
        .Unwrap();
      tasks.Add(t);
    }

    Dictionary<Id, NodeInfo>[] taskClosures = [];
    if (tasks.Count > 0)
    {
      taskClosures = await Task.WhenAll(tasks).ConfigureAwait(true);
    }
    var childClosures = _childClosurePool.Get();
    foreach (var childClosure in taskClosures)
    {
      foreach (var kvp in childClosure)
      {
        childClosures[kvp.Key] = kvp.Value;
      }
      _currentClosurePool.Return(childClosure);
    }

    var items = Serialise(obj, childClosures, cancellationTokenSource.Token);

    var currentClosures = _currentClosurePool.Get();
    Interlocked.Increment(ref _objectCount);
    progress?.Report(new(ProgressEvent.FromCacheOrSerialized, _objectCount, Math.Max(_objectCount, _objectsFound)));
    foreach (var item in items)
    {
      if (item.NeedsStorage)
      {
        Interlocked.Increment(ref _objectsSerialized);
        await Save(item, cancellationTokenSource.Token).ConfigureAwait(true);
        if (cancellationTokenSource.IsCancellationRequested)
        {
          return [];
        }
      }

      if (!currentClosures.ContainsKey(item.Id))
      {
        currentClosures.Add(item.Id, new NodeInfo(item.Json, item.Closures));
      }
    }
    _childClosurePool.Return(childClosures);
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

  public override async Task SendToServer(Batch<BaseItem> batch, CancellationToken cancellationToken)
  {
    try
    {
      if (!_options.SkipServer && batch.Items.Count != 0)
      {
        var objectBatch = batch.Items.Distinct().ToList();
        var hasObjects = await serverObjectManager
          .HasObjects(objectBatch.Select(x => x.Id.Value).Freeze(), cancellationToken)
          .ConfigureAwait(false);
        objectBatch = batch.Items.Where(x => !hasObjects[x.Id.Value]).ToList();
        if (objectBatch.Count != 0)
        {
          await serverObjectManager.UploadObjects(objectBatch, true, progress, cancellationToken).ConfigureAwait(false);
          Interlocked.Exchange(ref _uploaded, _uploaded + batch.Items.Count);
        }

        progress?.Report(new(ProgressEvent.UploadedObjects, _uploaded, null));
      }
    }
#pragma warning disable CA1031
    catch (Exception e)
#pragma warning restore CA1031
    {
      _logger.LogError(e, "Error sending objects to server");
      throw;
    }
  }

  public override void SaveToCache(List<BaseItem> batch)
  {
    try
    {
      if (!_options.SkipCacheWrite && batch.Count != 0)
      {
        sqLiteJsonCacheManager.SaveObjects(batch.Select(x => (x.Id.Value, x.Json.Value)));
        Interlocked.Exchange(ref _cached, _cached + batch.Count);
        progress?.Report(new(ProgressEvent.CachedToLocal, _cached, _objectsSerialized));
      }
    }
#pragma warning disable CA1031
    catch (Exception e)
#pragma warning restore CA1031
    {
      _logger.LogError(e, "Error sending objects to server");
      throw;
    }
  }
}
