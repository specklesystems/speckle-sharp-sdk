using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Speckle.InterfaceGenerator;
using Speckle.Sdk.Dependencies;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation.Utilities;
using Speckle.Sdk.SQLite;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Serialisation.V2.Receive;

public record DeserializeProcessOptions(
  bool SkipCache = false,
  bool ThrowOnMissingReferences = true,
  bool SkipInvalidConverts = false,
  int? MaxParallelism = null
);

public partial interface IDeserializeProcess : IDisposable;

[GenerateAutoInterface]
public sealed class DeserializeProcess(
  IObjectLoader objectLoader,
  IProgress<ProgressArgs>? progress,
  IBaseDeserializer baseDeserializer,
  ILoggerFactory loggerFactory,
  CancellationToken cancellationToken,
  DeserializeProcessOptions? options = null
) : IDeserializeProcess
{
  public DeserializeProcess(
    ISqLiteJsonCacheManager sqLiteJsonCacheManager,
    IServerObjectManager serverObjectManager,
    IProgress<ProgressArgs>? progress,
    IBaseDeserializer baseDeserializer,
    ILoggerFactory loggerFactory,
    CancellationToken cancellationToken,
    DeserializeProcessOptions? options = null
  )
    :
#pragma warning disable CA2000
  this(
      new ObjectLoader(sqLiteJsonCacheManager, serverObjectManager, progress, cancellationToken),
      progress,
      baseDeserializer,
      loggerFactory,
      cancellationToken,
      options
    )
#pragma warning restore CA2000
  { }

  private readonly PriorityScheduler _belowNormal = new(
    loggerFactory.CreateLogger<PriorityScheduler>(),
    ThreadPriority.BelowNormal,
    Environment.ProcessorCount * 2,
    cancellationToken
  );

  private readonly DeserializeProcessOptions _options = options ?? new();

  private readonly ConcurrentDictionary<Id, (Json, IReadOnlyCollection<Id>)> _closures = new();
  private readonly ConcurrentDictionary<Id, Base> _baseCache = new();
  private readonly ConcurrentDictionary<Id, Task> _activeTasks = new();

  public IReadOnlyDictionary<Id, Base> BaseCache => _baseCache;
  public long Total { get; private set; }

  [AutoInterfaceIgnore]
  public void Dispose()
  {
    objectLoader.Dispose();
    _belowNormal.Dispose();
  }

  /// <summary>
  /// All meaningful ids in the upcoming version
  /// </summary>
  private IReadOnlyCollection<Id> _allIds = [];

  public async Task<Base> Deserialize(string rootId)
  {
    var (rootJson, childrenIds) = await objectLoader.GetAndCache(rootId, _options).ConfigureAwait(false);
    var root = new Id(rootId);
    //childrenIds is already frozen but need to just add root?
    _allIds = childrenIds.Concat([root]).Freeze();
    Total = childrenIds.Count;
    Total++;
    _closures.TryAdd(root, (rootJson, childrenIds));
    progress?.Report(new(ProgressEvent.DeserializeObject, _baseCache.Count, childrenIds.Count));
    await Traverse(root).ConfigureAwait(false);
    await _belowNormal.WaitForCompletion().ConfigureAwait(false);
    return _baseCache[root];
  }

  private async Task Traverse(Id id)
  {
    // It doesn't make sense to try traverse id if it is not in the root, if this is the case object is serialized wrong in the first place.
    // This happened with datachunks that having weird __closures
    if (!_allIds.Contains(id))
    {
      return;
    }

    if (_baseCache.ContainsKey(id))
    {
      return;
    }
    var (_, childIds) = GetClosures(id);
    var tasks = new List<Task>();
    foreach (var childId in childIds)
    {
      if (_baseCache.ContainsKey(childId))
      {
        continue;
      }

      if (_activeTasks.TryGetValue(childId, out var task))
      {
        tasks.Add(task);
      }
      else
      {
        // tmp is necessary because of the way closures close over loop variables
        var tmpId = childId;
        cancellationToken.ThrowIfCancellationRequested();
        Task t = Task
          .Factory.StartNew(
            () => Traverse(tmpId),
            cancellationToken,
            TaskCreationOptions.AttachedToParent | TaskCreationOptions.PreferFairness,
            _belowNormal
          )
          .Unwrap();
        tasks.Add(t);
        _activeTasks.TryAdd(childId, t);
      }
    }

    if (tasks.Count > 0)
    {
      await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    //don't redo things if the id is decoded already in the cache
    if (!_baseCache.ContainsKey(id))
    {
      DecodeOrEnqueueChildren(id);
      progress?.Report(new(ProgressEvent.DeserializeObject, _baseCache.Count, Total));
    }
  }

  private (Json, IReadOnlyCollection<Id>) GetClosures(Id id)
  {
    if (!_closures.TryGetValue(id, out var closures))
    {
      var j = objectLoader.LoadId(id.Value);

      if (j == null)
      {
        throw new SpeckleException($"Missing object id in SQLite cache: {id}");
      }

      var json = new Json(j);
      var childrenIds = ClosureParser
        .GetClosures(json.Value, cancellationToken)
        .OrderByDescending(x => x.Item2)
        .Select(x => new Id(x.Item1))
        .Freeze();
      closures = (json, childrenIds);
      _closures.TryAdd(id, closures);
    }

    return closures;
  }

  public void DecodeOrEnqueueChildren(Id id)
  {
    if (_baseCache.ContainsKey(id))
    {
      return;
    }
    (Json json, IReadOnlyCollection<Id> closures) = GetClosures(id);
    var @base = baseDeserializer.Deserialise(_baseCache, id, json, closures, cancellationToken);
    _baseCache.TryAdd(id, @base);
    //remove from JSON cache because we've finally made the Base
    _closures.TryRemove(id, out _);
    _activeTasks.TryRemove(id, out _);
  }
}
