using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Speckle.InterfaceGenerator;
using Speckle.Sdk.Common;
using Speckle.Sdk.Dependencies;
using Speckle.Sdk.Models;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Serialisation.V2.Send;

public record SerializeProcessOptions(
  bool SkipCacheRead = false,
  bool SkipCacheWrite = false,
  bool SkipServer = false,
  bool SkipFindTotalObjects = false
)
{
  public int? MaxHttpSendBatchSize { get; set; }
  public int? MaxCacheBatchSize { get; set; }
  public int? MaxParallelism { get; set; }
}

public readonly record struct SerializeProcessResults(
  string RootId,
  IReadOnlyDictionary<Id, ObjectReference> ConvertedReferences
);

public interface ISerializeProcess : IAsyncDisposable
{
  Task<SerializeProcessResults> Serialize(Base root);
}

public sealed class SerializeProcess(
  IProgress<ProgressArgs>? progress,
  IObjectSaver objectSaver,
  IBaseChildFinder baseChildFinder,
  IBaseSerializer baseSerializer,
  ILoggerFactory loggerFactory,
  CancellationToken cancellationToken,
  SerializeProcessOptions? options = null
) : ISerializeProcess
{
  private static readonly Dictionary<Id, NodeInfo> EMPTY_CLOSURES = new();

  private readonly CancellationTokenSource _processSource = CancellationTokenSource.CreateLinkedTokenSource(
    cancellationToken
  );
  private readonly ILogger<SerializeProcess> _logger = loggerFactory.CreateLogger<SerializeProcess>();
  private bool _disposed;

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

  private readonly Pool<Dictionary<Id, NodeInfo>> _currentClosurePool = Pools.CreateDictionaryPool<Id, NodeInfo>();
  private readonly Pool<ConcurrentDictionary<Id, NodeInfo>> _childClosurePool = Pools.CreateConcurrentDictionaryPool<
    Id,
    NodeInfo
  >();

  private readonly Pool<List<Task<Dictionary<Id, NodeInfo>>>> _taskResultPool = Pools.CreateListPool<
    Task<Dictionary<Id, NodeInfo>>
  >();

  private long _objectCount;
  private long _objectsFound;

  private long _objectsSerialized;

  [AutoInterfaceIgnore]
  public async ValueTask DisposeAsync()
  {
    _disposed = true;
    await WaitForSchedulerCompletion().ConfigureAwait(false);
    await _highest.DisposeAsync().ConfigureAwait(false);
    await _belowNormal.DisposeAsync().ConfigureAwait(false);
    objectSaver.Dispose();
    _processSource.Dispose();
  }

  private void ThrowIfFailed()
  {
    //order here matters...null with cancellation means a user did it, otherwise it's a real Exception
    if (objectSaver.Exception is not null)
    {
      throw new SpeckleException($"Error while sending: {objectSaver.Exception.Message}", objectSaver.Exception);
    }
    _processSource.Token.ThrowIfCancellationRequested();
  }

  private async Task WaitForSchedulerCompletion()
  {
    await _highest.WaitForCompletion().ConfigureAwait(false);
    await _belowNormal.WaitForCompletion().ConfigureAwait(false);
  }

  public async Task<SerializeProcessResults> Serialize(Base root)
  {
    try
    {
      var channelTask = objectSaver.Start(
        options?.MaxParallelism,
        options?.MaxHttpSendBatchSize,
        options?.MaxCacheBatchSize,
        _processSource.Token
      );
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

      await Traverse(root).ConfigureAwait(false);
      ThrowIfFailed();
      objectSaver.DoneTraversing();
      await Task.WhenAll(findTotalObjectsTask, channelTask).ConfigureAwait(false);
      ThrowIfFailed();
      await objectSaver.DoneSaving().ConfigureAwait(false);
      ThrowIfFailed();
      await WaitForSchedulerCompletion().ConfigureAwait(false);
      ThrowIfFailed();
      return new(root.id.NotNull(), baseSerializer.ObjectReferences.Freeze());
    }
    catch (OperationCanceledException)
    {
      ThrowIfFailed();
      throw;
    }
  }

  private void TraverseTotal(Base obj)
  {
    if (IsCancelled())
    {
      return;
    }
    foreach (var child in baseChildFinder.GetChildren(obj))
    {
      Interlocked.Increment(ref _objectsFound);
      progress?.Report(new(ProgressEvent.FindingChildren, _objectsFound, null));
      TraverseTotal(child);
    }
  }

  private async Task<Dictionary<Id, NodeInfo>> Traverse(Base obj)
  {
    if (IsCancelled())
    {
      return EMPTY_CLOSURES;
    }

    try
    {
      var tasks = _taskResultPool.Get();
      foreach (var child in baseChildFinder.GetChildren(obj))
      {
        // tmp is necessary because of the way closures close over loop variables
        var tmp = child;
        if (IsCancelled())
        {
          return EMPTY_CLOSURES;
        }
        var t = Task
          .Factory.StartNew(
            // ReSharper disable once AccessToDisposedClosure
            // don't need to capture here
            async () => await Traverse(tmp).ConfigureAwait(false),
            _processSource.Token,
            TaskCreationOptions.AttachedToParent | TaskCreationOptions.PreferFairness,
            _belowNormal
          )
          .Unwrap();
        tasks.Add(t);
      }

      if (IsCancelled())
      {
        return EMPTY_CLOSURES;
      }

      Dictionary<Id, NodeInfo>[] taskClosures = [];
      if (tasks.Count > 0)
      {
        //get child results
        var childTask = Task.WhenAll(tasks);
        await Task.WhenAny(childTask, Task.Delay(Timeout.InfiniteTimeSpan, _processSource.Token)).ConfigureAwait(false);
        if (childTask.IsFaulted)
        {
          if (childTask.Exception is not null)
          {
            RecordException(childTask.Exception);
          }
          return EMPTY_CLOSURES;
        }
        if (!childTask.IsCompleted)
        {
          return EMPTY_CLOSURES;
        }
        taskClosures = childTask.Result;
      }
      _taskResultPool.Return(tasks);

      if (IsCancelled())
      {
        return EMPTY_CLOSURES;
      }

      var childClosures = _childClosurePool.Get();
      foreach (var childClosure in taskClosures)
      {
        if (IsCancelled())
        {
          return EMPTY_CLOSURES;
        }
        foreach (var kvp in childClosure)
        {
          childClosures[kvp.Key] = kvp.Value;
          if (IsCancelled())
          {
            return EMPTY_CLOSURES;
          }
        }

        _currentClosurePool.Return(childClosure);
      }

      if (IsCancelled())
      {
        return EMPTY_CLOSURES;
      }

      var items = baseSerializer.Serialise(obj, childClosures, _options.SkipCacheRead, _processSource.Token);

      if (IsCancelled())
      {
        return EMPTY_CLOSURES;
      }

      var currentClosures = _currentClosurePool.Get();
      try
      {
        Interlocked.Increment(ref _objectCount);
        progress?.Report(new(ProgressEvent.FromCacheOrSerialized, _objectCount, Math.Max(_objectCount, _objectsFound)));
        foreach (var item in items)
        {
          if (IsCancelled())
          {
            return EMPTY_CLOSURES;
          }

          if (item.NeedsStorage)
          {
            Interlocked.Increment(ref _objectsSerialized);
            await objectSaver.SaveAsync(item).ConfigureAwait(false);
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

      return currentClosures;
    }
    catch (OperationCanceledException)
    {
      return EMPTY_CLOSURES;
    }
#pragma warning disable CA1031
    catch (Exception e)
#pragma warning restore CA1031
    {
      RecordException(e);
      return EMPTY_CLOSURES;
    }
  }

  public bool IsCancelled() => _disposed || _processSource.IsCancellationRequested;

  public void RecordException(Exception e)
  {
    if (e is OperationCanceledException)
    {
      return;
    }

    if (
      e is AggregateException ae
      && ae.InnerExceptions.Count == ae.InnerExceptions.OfType<OperationCanceledException>().Count()
    )
    {
      return;
    }
    if (IsCancelled())
    {
      //if we are already cancelled, don't log or save the exceptions
      return;
    }
    //order here matters
    _logger.LogError(e, "Error in SDK: {message}", e.Message);
    objectSaver.Exception = e;
    _processSource.Cancel();
  }
}
