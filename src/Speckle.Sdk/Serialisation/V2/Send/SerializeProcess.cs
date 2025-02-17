using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Speckle.InterfaceGenerator;
using Speckle.Sdk.Common;
using Speckle.Sdk.Dependencies;
using Speckle.Sdk.Dependencies.Serialization;
using Speckle.Sdk.Models;
using Speckle.Sdk.SQLite;
using Speckle.Sdk.Transports;

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

public partial interface ISerializeProcess : IAsyncDisposable;

[GenerateAutoInterface]
public sealed class SerializeProcess(
  IProgress<ProgressArgs>? progress,
  ISqLiteJsonCacheManager sqLiteJsonCacheManager,
  IServerObjectManager serverObjectManager,
  IBaseChildFinder baseChildFinder,
  IBaseSerializer baseSerializer,
  ILoggerFactory loggerFactory,
  CancellationToken cancellationToken,
  SerializeProcessOptions? options = null
) : ChannelSaver<BaseItem>, ISerializeProcess
{
  //async dispose
  [SuppressMessage("Usage", "CA2213:Disposable fields should be disposed")]
  private readonly PriorityScheduler _highest = new(
    loggerFactory.CreateLogger<PriorityScheduler>(),
    ThreadPriority.Highest,
    2,
    cancellationToken
  );

  //async dispose
  [SuppressMessage("Usage", "CA2213:Disposable fields should be disposed")]
  private readonly PriorityScheduler _belowNormal = new(
    loggerFactory.CreateLogger<PriorityScheduler>(),
    ThreadPriority.BelowNormal,
    Environment.ProcessorCount * 2,
    cancellationToken
  );

  private readonly SerializeProcessOptions _options = options ?? new();
  private readonly ILogger<SerializeProcess> _logger = loggerFactory.CreateLogger<SerializeProcess>();

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
  public async ValueTask DisposeAsync()
  {
    _highest.Dispose();
    _belowNormal.Dispose();
    sqLiteJsonCacheManager.Dispose();
    await WaitForSchedulerCompletion().ConfigureAwait(false);
  }

  private async Task WaitForSchedulerCompletion()
  {
    await _highest.WaitForCompletion().ConfigureAwait(false);
    await _belowNormal.WaitForCompletion().ConfigureAwait(false);
  }

  public async Task<SerializeProcessResults> Serialize(Base root)
  {
    var channelTask = Start(cancellationToken);
    var findTotalObjectsTask = Task.CompletedTask;
    if (!_options.SkipFindTotalObjects)
    {
      cancellationToken.ThrowIfCancellationRequested();
      findTotalObjectsTask = Task.Factory.StartNew(
        () => TraverseTotal(root),
        cancellationToken,
        TaskCreationOptions.AttachedToParent | TaskCreationOptions.PreferFairness,
        _highest
      );
    }
    await Traverse(root).ConfigureAwait(false);
    await DoneTraversing().ConfigureAwait(false);
    await Task.WhenAll(findTotalObjectsTask, channelTask).ConfigureAwait(false);
    cancellationToken.ThrowIfCancellationRequested();
    await DoneSaving().ConfigureAwait(false);
    cancellationToken.ThrowIfCancellationRequested();
    await WaitForSchedulerCompletion().ConfigureAwait(false);
    cancellationToken.ThrowIfCancellationRequested();
    return new(root.id.NotNull(), baseSerializer.ObjectReferences.Freeze());
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

  private async Task<Dictionary<Id, NodeInfo>> Traverse(Base obj)
  {
    var tasks = new List<Task<Dictionary<Id, NodeInfo>>>();
    foreach (var child in baseChildFinder.GetChildren(obj))
    {
      // tmp is necessary because of the way closures close over loop variables
      var tmp = child;
      cancellationToken.ThrowIfCancellationRequested();
      var t = Task
        .Factory.StartNew(
          async () => await Traverse(tmp).ConfigureAwait(false),
          cancellationToken,
          TaskCreationOptions.AttachedToParent | TaskCreationOptions.PreferFairness,
          _belowNormal
        )
        .Unwrap();
      tasks.Add(t);
    }

    Dictionary<Id, NodeInfo>[] taskClosures = [];
    if (tasks.Count > 0)
    {
      taskClosures = await Task.WhenAll(tasks).ConfigureAwait(false);
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

    var items = baseSerializer.Serialise(obj, childClosures, _options.SkipCacheRead, cancellationToken);

    var currentClosures = _currentClosurePool.Get();
    Interlocked.Increment(ref _objectCount);
    progress?.Report(new(ProgressEvent.FromCacheOrSerialized, _objectCount, Math.Max(_objectCount, _objectsFound)));
    foreach (var item in items)
    {
      if (item.NeedsStorage)
      {
        Interlocked.Increment(ref _objectsSerialized);
        await Save(item, cancellationToken).ConfigureAwait(false);
      }

      if (!currentClosures.ContainsKey(item.Id))
      {
        currentClosures.Add(item.Id, new NodeInfo(item.Json, item.Closures));
      }
    }
    _childClosurePool.Return(childClosures);
    return currentClosures;
  }

  public override async Task SendToServer(Batch<BaseItem> batch)
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
    catch (OperationCanceledException)
    {
      throw;
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
    catch (OperationCanceledException)
    {
      throw;
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
