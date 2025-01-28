using System.Collections.Concurrent;
using Speckle.InterfaceGenerator;
using Speckle.Sdk.Dependencies;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation.Utilities;
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
  IProgress<ProgressArgs>? progress,
  IObjectLoader objectLoader,
  IObjectDeserializerFactory objectDeserializerFactory,
  CancellationToken cancellationToken,
  DeserializeProcessOptions? options = null
) : IDeserializeProcess
{
  private readonly DeserializeProcessOptions _options = options ?? new();

  private readonly ConcurrentDictionary<Id, (Json, IReadOnlyCollection<Id>)> _closures = new();
  private readonly ConcurrentDictionary<Id, Base> _baseCache = new();
  private readonly ConcurrentDictionary<Id, Task> _activeTasks = new();

  public IReadOnlyDictionary<Id, Base> BaseCache => _baseCache;
  public long Total { get; private set; }

  [AutoInterfaceIgnore]
  public void Dispose() => objectLoader.Dispose();

  public async Task<Base> Deserialize(string rootId)
  {
    var (rootJson, childrenIds) = await objectLoader
      .GetAndCache(rootId, _options, cancellationToken)
      .ConfigureAwait(false);
    Total = childrenIds.Count;
    Total++;
    var root = new Id(rootId);
    _closures.TryAdd(root, (rootJson, childrenIds));
    progress?.Report(new(ProgressEvent.DeserializeObject, _baseCache.Count, childrenIds.Count));
    await Traverse(root).ConfigureAwait(false);
    return _baseCache[root];
  }

  private async Task Traverse(Id id)
  {
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
            TaskScheduler.Default
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
    var @base = Deserialise(id, json, closures);
    _baseCache.TryAdd(id, @base);
    //remove from JSON cache because we've finally made the Base
    _closures.TryRemove(id, out _);
    _activeTasks.TryRemove(id, out _);
  }

  private Base Deserialise(Id id, Json json, IReadOnlyCollection<Id> closures)
  {
    if (_baseCache.TryGetValue(id, out var baseObject))
    {
      return baseObject;
    }

    var deserializer = objectDeserializerFactory.Create(id, closures, _baseCache);
    return deserializer.Deserialize(json, cancellationToken);
  }
}
