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
  //this listens to the user but also will cancel when the process fails

  private readonly CancellationTokenSource _processSource = CancellationTokenSource.CreateLinkedTokenSource(
    cancellationToken
  );

  //async dispose
  [SuppressMessage("Usage", "CA2213:Disposable fields should be disposed")]
  private readonly PriorityScheduler _highest = new(
    loggerFactory.CreateLogger<PriorityScheduler>(),
    ThreadPriority.Highest,
    2
  );

  //async dispose
  [SuppressMessage("Usage", "CA2213:Disposable fields should be disposed")]
  private readonly PriorityScheduler _belowNormal = new(
    loggerFactory.CreateLogger<PriorityScheduler>(),
    ThreadPriority.BelowNormal,
    Environment.ProcessorCount * 2
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
    await WaitForSchedulerCompletion().ConfigureAwait(true);
    await _highest.DisposeAsync().ConfigureAwait(true);
    await _belowNormal.DisposeAsync().ConfigureAwait(true);
    sqLiteJsonCacheManager.Dispose();
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

      await TryTraverse(root, _processSource.Token).ConfigureAwait(true);
      ThrowIfFailed();
      DoneTraversing();
      await Task.WhenAll(findTotalObjectsTask, channelTask).ConfigureAwait(true);
      ThrowIfFailed();
      await DoneSaving().ConfigureAwait(true);
      ThrowIfFailed();
      await WaitForSchedulerCompletion().ConfigureAwait(true);
      ThrowIfFailed();
      return new(root.id.NotNull(), baseSerializer.ObjectReferences.Freeze());
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
    foreach (var child in baseChildFinder.GetChildren(obj))
    {
      _objectsFound++;
      progress?.Report(new(ProgressEvent.FindingChildren, _objectsFound, null));
      TraverseTotal(child);
    }
  }

  private async Task<(bool, Dictionary<Id, NodeInfo>)> TryTraverse(Base obj, CancellationToken token)
  {
    if (token.IsCancellationRequested)
    {
      return (false, new Dictionary<Id, NodeInfo>());
    }

    try
    {
      var tasks = new List<Task<(bool, Dictionary<Id, NodeInfo>)>>();
      using var childCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);
      foreach (var child in baseChildFinder.GetChildren(obj))
      {
        // tmp is necessary because of the way closures close over loop variables
        var tmp = child;
        if (token.IsCancellationRequested)
        {
          return (false, new Dictionary<Id, NodeInfo>());
        }
        var t = Task
          .Factory.StartNew(
            // ReSharper disable once AccessToDisposedClosure
            // don't need to capture here
            async () => await TryTraverse(tmp, childCancellationTokenSource.Token).ConfigureAwait(true),
            childCancellationTokenSource.Token,
            TaskCreationOptions.AttachedToParent | TaskCreationOptions.PreferFairness,
            _belowNormal
          )
          .Unwrap();
        tasks.Add(t);
      }

      if (token.IsCancellationRequested)
      {
        return (false, new Dictionary<Id, NodeInfo>());
      }

      List<Dictionary<Id, NodeInfo>> taskClosures = new();
      if (tasks.Count > 0)
      {
        var currentTasks = tasks.ToList();
        do
        {
          //grab when any Task is done and see if we're cancelling
          var t = await Task.WhenAny(currentTasks).ConfigureAwait(true);
          if (t.IsCanceled)
          {
            return (false, new Dictionary<Id, NodeInfo>());
          }
          if (t.IsFaulted)
          {
            if (t.Exception is not null)
            {
              RecordException(t.Exception);
            }
            return (false, new Dictionary<Id, NodeInfo>());
          }
          var (success, results) = t.Result;
          if (!success)
          {
            return (false, new Dictionary<Id, NodeInfo>());
          }

          taskClosures.Add(results);
          currentTasks.Remove(t);
        } while (currentTasks.Count > 0);
      }

      if (token.IsCancellationRequested)
      {
        return (false, new Dictionary<Id, NodeInfo>());
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

      if (token.IsCancellationRequested)
      {
        return (false, new Dictionary<Id, NodeInfo>());
      }

      var items = baseSerializer.Serialise(obj, childClosures, _options.SkipCacheRead, token);
      if (token.IsCancellationRequested)
      {
        return (false, new Dictionary<Id, NodeInfo>());
      }

      var currentClosures = _currentClosurePool.Get();
      try
      {
        Interlocked.Increment(ref _objectCount);
        progress?.Report(new(ProgressEvent.FromCacheOrSerialized, _objectCount, Math.Max(_objectCount, _objectsFound)));
        foreach (var item in items)
        {
          if (item.NeedsStorage)
          {
            if (token.IsCancellationRequested)
            {
              return (false, new Dictionary<Id, NodeInfo>());
            }

            Interlocked.Increment(ref _objectsSerialized);
            Save(item, childCancellationTokenSource.Token);
          }

          if (!currentClosures.ContainsKey(item.Id))
          {
            currentClosures.Add(item.Id, new NodeInfo(item.Json, item.Closures));
          }
        }
      }
      finally
      {
        _childClosurePool.Return(childClosures);
      }

      return (true, currentClosures);
    }
    catch (OperationCanceledException)
    {
      return (false, new Dictionary<Id, NodeInfo>());
    }
#pragma warning disable CA1031
    catch (Exception e)
#pragma warning restore CA1031
    {
      RecordException(e);
      return (false, new Dictionary<Id, NodeInfo>());
    }
  }

  protected override async Task SendToServerInternal(Batch<BaseItem> batch)
  {
    if (_processSource.IsCancellationRequested)
    {
      return;
    }
    try
    {
      if (!_options.SkipServer && batch.Items.Count != 0)
      {
        var objectBatch = batch.Items.Distinct().ToList();
        var hasObjects = await serverObjectManager
          .HasObjects(objectBatch.Select(x => x.Id.Value).Freeze(), _processSource.Token)
          .ConfigureAwait(true);
        objectBatch = batch.Items.Where(x => !hasObjects[x.Id.Value]).ToList();
        if (objectBatch.Count != 0)
        {
          await serverObjectManager
            .UploadObjects(objectBatch, true, progress, _processSource.Token)
            .ConfigureAwait(true);
          Interlocked.Exchange(ref _uploaded, _uploaded + batch.Items.Count);
        }

        progress?.Report(new(ProgressEvent.UploadedObjects, _uploaded, null));
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
        sqLiteJsonCacheManager.SaveObjects(batch.Select(x => (x.Id.Value, x.Json.Value)));
        Interlocked.Exchange(ref _cached, _cached + batch.Count);
        progress?.Report(new(ProgressEvent.CachedToLocal, _cached, _objectsSerialized));
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
