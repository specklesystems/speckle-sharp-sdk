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
public sealed class SerializeProcess: ChannelSaver<BaseItem>, ISerializeProcess
{
  private readonly IProgress<ProgressArgs>? _progress;
  private readonly ISqLiteJsonCacheManager _sqLiteJsonCacheManager;
  private readonly IServerObjectManager _serverObjectManager;
  private readonly IBaseChildFinder _baseChildFinder;
  private readonly IBaseSerializer _baseSerializer;

  public SerializeProcess(
    IProgress<ProgressArgs>? progress,
    ISqLiteJsonCacheManager sqLiteJsonCacheManager,
    IServerObjectManager serverObjectManager,
    IBaseChildFinder baseChildFinder,
    IBaseSerializer baseSerializer,
    ILoggerFactory loggerFactory,
    CancellationToken cancellationToken,
    SerializeProcessOptions? options = null
  ) 
  {
    _progress = progress;
    _sqLiteJsonCacheManager = sqLiteJsonCacheManager;
    _serverObjectManager = serverObjectManager;
    _baseChildFinder = baseChildFinder;
    _baseSerializer = baseSerializer;
    _options = options ?? new();
    _logger = loggerFactory.CreateLogger<SerializeProcess>();
    //this listens to the user but also will cancel when the process fails
    _processSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    
    _highest = new(
      loggerFactory.CreateLogger<PriorityScheduler>(),
      ThreadPriority.Highest,
      2,
      _processSource.Token
    );
    _belowNormal = new(
      loggerFactory.CreateLogger<PriorityScheduler>(),
      ThreadPriority.BelowNormal,
      Environment.ProcessorCount * 2,
      _processSource.Token
    );
  }

  private readonly CancellationTokenSource _processSource;
  //async dispose
  [SuppressMessage("Usage", "CA2213:Disposable fields should be disposed")]
  private readonly PriorityScheduler _highest;

  //async dispose
  [SuppressMessage("Usage", "CA2213:Disposable fields should be disposed")]
  private readonly PriorityScheduler _belowNormal;
  private readonly SerializeProcessOptions _options;
  private readonly ILogger<SerializeProcess> _logger;

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
    await WaitForSchedulerCompletion().ConfigureAwait(true);
    await _highest.DisposeAsync().ConfigureAwait(true);
    await _belowNormal.DisposeAsync().ConfigureAwait(true);
    _sqLiteJsonCacheManager.Dispose();
    _processSource.Dispose();
  }

  private void ThrowIfFailed()
  {
    //order here matters...null with cancellation means a user did it, otherwise it's a real Exception
    if (Exception is not null)
    {
      throw new SpeckleException("Error while sending", Exception);
    }
    _processSource.Token.ThrowIfCancellationRequested();
  }

  private async Task WaitForSchedulerCompletion()
  {
    await _highest.WaitForCompletion().ConfigureAwait(true);
    await _belowNormal.WaitForCompletion().ConfigureAwait(true);
  }

  public async Task<SerializeProcessResults> Serialize(Base root)
  {
    try
    {
      var channelTask = Start(_processSource.Token);
      var findTotalObjectsTask = Task.CompletedTask;
      if (!_options.SkipFindTotalObjects)
      {
        ThrowIfFailed();
        findTotalObjectsTask = Task.Factory.StartNew(
          () => TraverseTotal(root),
          _processSource.Token,
          TaskCreationOptions.AttachedToParent | TaskCreationOptions.PreferFairness,
          _highest
        );
      }

      await TryTraverse(root).ConfigureAwait(true);
      ThrowIfFailed();
      DoneTraversing();
      await Task.WhenAll(findTotalObjectsTask, channelTask).ConfigureAwait(true);
      ThrowIfFailed();
      await DoneSaving().ConfigureAwait(true);
      ThrowIfFailed();
      await WaitForSchedulerCompletion().ConfigureAwait(true);
      ThrowIfFailed();
      return new(root.id.NotNull(), _baseSerializer.ObjectReferences.Freeze());
    }
    catch (TaskCanceledException)
    {
      ThrowIfFailed();
      throw;
    }
  }

  private void TraverseTotal(Base obj)
  { 
    if (_processSource.Token.IsCancellationRequested)
    {
      return;
    }
    foreach (var child in _baseChildFinder.GetChildren(obj))
    {
      _objectsFound++;
      _progress?.Report(new(ProgressEvent.FindingChildren, _objectsFound, null));
      TraverseTotal(child);
    }
  }

  private async Task<Dictionary<Id, NodeInfo>> TryTraverse(Base obj)
  {
    if (_processSource.Token.IsCancellationRequested)
    {
      return new Dictionary<Id, NodeInfo>();
    }
    try
    {
      var tasks = new List<Task<Dictionary<Id, NodeInfo>>>();
      foreach (var child in _baseChildFinder.GetChildren(obj))
      {
        // tmp is necessary because of the way closures close over loop variables
        var tmp = child;
        if (_processSource.Token.IsCancellationRequested)
        {
          return new Dictionary<Id, NodeInfo>();
        }
        var t = Task
          .Factory.StartNew(
            async () => await TryTraverse(tmp).ConfigureAwait(true),
            _processSource.Token,
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
      if (_processSource.Token.IsCancellationRequested)
      {
        return new Dictionary<Id, NodeInfo>();
      }
      var childClosures = _childClosurePool.Get();
      foreach (var childClosure in taskClosures)
      {
        if (_processSource.Token.IsCancellationRequested)
        {
          return new Dictionary<Id, NodeInfo>();
        }
        foreach (var kvp in childClosure)
        {
          childClosures[kvp.Key] = kvp.Value;
        }

        _currentClosurePool.Return(childClosure);
      }

      var items = _baseSerializer.Serialise(obj, childClosures, _options.SkipCacheRead, _processSource.Token);
      if (_processSource.Token.IsCancellationRequested)
      {
        return new Dictionary<Id, NodeInfo>();
      }
      var currentClosures = _currentClosurePool.Get();
      Interlocked.Increment(ref _objectCount);
      _progress?.Report(new(ProgressEvent.FromCacheOrSerialized, _objectCount, Math.Max(_objectCount, _objectsFound)));
      foreach (var item in items)
      {if (_processSource.Token.IsCancellationRequested)
        {
          return new Dictionary<Id, NodeInfo>();
        }
        if (item.NeedsStorage)
        {
          Interlocked.Increment(ref _objectsSerialized);
          Save(item, _processSource.Token);
        }

        if (!currentClosures.ContainsKey(item.Id))
        {
          currentClosures.Add(item.Id, new NodeInfo(item.Json, item.Closures));
        }
      }

      _childClosurePool.Return(childClosures);
      return currentClosures;
    }
#pragma warning disable CA1031
    catch (Exception e)
#pragma warning restore CA1031
    {
      RecordException(e);
      return new Dictionary<Id, NodeInfo>();
    }
  }

  protected override async Task SendToServerInternal(Batch<BaseItem> batch)
  { if (_processSource.IsCancellationRequested)
    {
      return;
    }
    try
    {
      if (!_options.SkipServer && batch.Items.Count != 0)
      {
        var objectBatch = batch.Items.Distinct().ToList();
        var hasObjects = await _serverObjectManager
          .HasObjects(objectBatch.Select(x => x.Id.Value).Freeze(), _processSource.Token)
          .ConfigureAwait(true);
        objectBatch = batch.Items.Where(x => !hasObjects[x.Id.Value]).ToList();
        if (objectBatch.Count != 0)
        {
          await _serverObjectManager.UploadObjects(objectBatch, true, _progress, _processSource.Token).ConfigureAwait(true);
          Interlocked.Exchange(ref _uploaded, _uploaded + batch.Items.Count);
        }

        _progress?.Report(new(ProgressEvent.UploadedObjects, _uploaded, null));
      }
    }
    catch (OperationCanceledException)
    {
      _processSource.Cancel();
    }
#pragma warning disable CA1031
    catch (Exception e)
#pragma warning restore CA1031
    {
      RecordException(e);
    }
  }

  public override void SaveToCache(List<BaseItem> batch)
  {
    if (_processSource.IsCancellationRequested)
    {
      return;
    }
    try
    {
      if (!_options.SkipCacheWrite && batch.Count != 0)
      {
        _sqLiteJsonCacheManager.SaveObjects(batch.Select(x => (x.Id.Value, x.Json.Value)));
        Interlocked.Exchange(ref _cached, _cached + batch.Count);
        _progress?.Report(new(ProgressEvent.CachedToLocal, _cached, _objectsSerialized));
      }
    }
    catch (OperationCanceledException)
    {
      _processSource.Cancel();
    }
#pragma warning disable CA1031
    catch (Exception e)
#pragma warning restore CA1031
    {
      RecordException(e);
    }
  }

  private void RecordException(Exception e)
  {
    //order here matters
    _logger.LogError(e, "Error in SDK");
    Exception = e;
    _processSource.Cancel();
  }
}
