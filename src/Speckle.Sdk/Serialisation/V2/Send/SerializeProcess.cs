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
  }

  public void ThrowIfFailed()
  {
    //always check for cancellation first
    cancellationToken.ThrowIfCancellationRequested();
    if (Exception is not null)
    {
      throw new SpeckleException("Error while sending", objectSaver.Exception);
    }
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
      var channelTask = objectSaver.Start();
      var findTotalObjectsTask = Task.CompletedTask;
      if (!_options.SkipFindTotalObjects)
      {
        ThrowIfFailed();
        findTotalObjectsTask = Task.Factory.StartNew(
          () => TraverseTotal(root),
          cancellationToken,
          TaskCreationOptions.AttachedToParent | TaskCreationOptions.PreferFairness,
          _highest
        );
      }

      await Traverse(root).ConfigureAwait(false);
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
    foreach (var item in items.DistinctBy(x => x.Id))
    {
      if (item.NeedsStorage)
      {
        Interlocked.Increment(ref _objectsSerialized);
        await objectSaver.SaveItem(item).ConfigureAwait(false);
      }

      if (!currentClosures.ContainsKey(item.Id))
      {
        currentClosures.Add(item.Id, new NodeInfo(item.Json, item.Closures));
      }
    }
    _childClosurePool.Return(childClosures);
    return currentClosures;
  }
}
