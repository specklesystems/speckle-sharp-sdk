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
);

public readonly record struct SerializeProcessResults(
  string RootId,
  IReadOnlyDictionary<Id, ObjectReference> ConvertedReferences
);

public partial interface ISerializeProcess : IAsyncDisposable;

[GenerateAutoInterface]
public sealed class SerializeProcess(
  IProgress<ProgressArgs>? progress,
  IObjectSaver objectSaver,
  IBaseChildFinder baseChildFinder,
  IBaseSerializer baseSerializer,
  ILoggerFactory loggerFactory,
  CancellationToken cancellationToken,
  SerializeProcessOptions? options = null
#pragma warning disable CS9107
#pragma warning disable CA2254
) : ISerializeProcess
#pragma warning restore CA2254
#pragma warning restore CS9107
{
  private static readonly Dictionary<Id, NodeInfo> EMPTY_CLOSURES = new();

  private readonly CancellationTokenSource _processSource = CancellationTokenSource.CreateLinkedTokenSource(
    cancellationToken
  );
  private readonly ILogger<SerializeProcess> _logger = loggerFactory.CreateLogger<SerializeProcess>();

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

  private long _objectCount;
  private long _objectsFound;

  private long _objectsSerialized;

  [AutoInterfaceIgnore]
  public async ValueTask DisposeAsync()
  {
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
      throw new SpeckleException("Error while sending", objectSaver.Exception);
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
      var channelTask = objectSaver.Start(_processSource.Token);
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

      await Traverse(root, _processSource.Token).ConfigureAwait(false);
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

  private async Task<Dictionary<Id, NodeInfo>> Traverse(Base obj, CancellationToken token)
  {
    if (token.IsCancellationRequested)
    {
      return EMPTY_CLOSURES;
    }

    try
    {
      var tasks = new List<Task<Dictionary<Id, NodeInfo>>>();
      using var childCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);
      foreach (var child in baseChildFinder.GetChildren(obj))
      {
        // tmp is necessary because of the way closures close over loop variables
        var tmp = child;
        if (token.IsCancellationRequested)
        {
          return EMPTY_CLOSURES;
        }
        var t = Task
          .Factory.StartNew(
            // ReSharper disable once AccessToDisposedClosure
            // don't need to capture here
            async () => await Traverse(tmp, childCancellationTokenSource.Token).ConfigureAwait(false),
            childCancellationTokenSource.Token,
            TaskCreationOptions.AttachedToParent | TaskCreationOptions.PreferFairness,
            _belowNormal
          )
          .Unwrap();
        tasks.Add(t);
      }

      if (token.IsCancellationRequested)
      {
        return EMPTY_CLOSURES;
      }

      List<Dictionary<Id, NodeInfo>> taskClosures = new();
      if (tasks.Count > 0)
      {
        var currentTasks = tasks.ToList();
        do
        {
          //grab when any Task is done and see if we're cancelling
          var t = await Task.WhenAny(currentTasks).ConfigureAwait(false);
          if (t.IsCanceled)
          {
            return EMPTY_CLOSURES;
          }
          if (t.IsFaulted)
          {
            if (t.Exception is not null)
            {
              RecordException(t.Exception);
            }
            return EMPTY_CLOSURES;
          }
          taskClosures.Add(t.Result);
          currentTasks.Remove(t);
        } while (currentTasks.Count > 0);
      }

      if (token.IsCancellationRequested)
      {
        return EMPTY_CLOSURES;
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
        return EMPTY_CLOSURES;
      }

      var items = baseSerializer.Serialise(obj, childClosures, _options.SkipCacheRead, token);
      if (token.IsCancellationRequested)
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
          if (item.NeedsStorage)
          {
            if (token.IsCancellationRequested)
            {
              return EMPTY_CLOSURES;
            }

            Interlocked.Increment(ref _objectsSerialized);
            objectSaver.SaveItem(item, childCancellationTokenSource.Token);
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

  private void RecordException(Exception e)
  {
    //order here matters
    _logger.LogError(e, "Error in SDK");
    objectSaver.Exception = e;
    _processSource.Cancel();
  }
}
