using System.Collections.Concurrent;
using Speckle.InterfaceGenerator;
using Speckle.Sdk.Dependencies;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation.Utilities;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Serialisation.V2.Receive;

public record DeserializeProcessOptions(
  bool SkipCache,
  bool ThrowOnMissingReferences = true,
  bool SkipInvalidConverts = false
);

[GenerateAutoInterface]
public sealed class DeserializeProcess(
  IProgress<ProgressArgs>? progress,
  IObjectLoader objectLoader,
  IObjectDeserializerFactory objectDeserializerFactory,
  DeserializeProcessOptions? options = null
) : IDeserializeProcess
{
  private readonly DeserializeProcessOptions _options = options ?? new(false);

  private readonly ConcurrentDictionary<string, (string, IReadOnlyCollection<string>)> _closures = new();
  private readonly ConcurrentDictionary<string, Base> _baseCache = new();
  private readonly ConcurrentDictionary<string, Task> _activeTasks = new();

  public IReadOnlyDictionary<string, Base> BaseCache => _baseCache;
  public long Total { get; private set; }

  public async Task<Base> Deserialize(string rootId, CancellationToken cancellationToken)
  {
    var (rootJson, childrenIds) = await objectLoader
      .GetAndCache(rootId, _options, cancellationToken)
      .ConfigureAwait(false);
    Total = childrenIds.Count;
    Total++;
    _closures.TryAdd(rootId, (rootJson, childrenIds));
    progress?.Report(new(ProgressEvent.DeserializeObject, _baseCache.Count, childrenIds.Count));
    await Traverse(rootId, cancellationToken).ConfigureAwait(false);
    return _baseCache[rootId];
  }

  private async Task Traverse(string id, CancellationToken cancellationToken)
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
        Task t = Task
          .Factory.StartNew(
            () => Traverse(tmpId, cancellationToken),
            cancellationToken,
            TaskCreationOptions.AttachedToParent,
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

  private (string, IReadOnlyCollection<string>) GetClosures(string id)
  {
    if (!_closures.TryGetValue(id, out var closures))
    {
      var json = objectLoader.LoadId(id);
      if (json == null)
      {
        throw new SpeckleException($"Missing object id in SQLite cache: {id}");
      }
      var childrenIds = ClosureParser.GetClosures(json).OrderByDescending(x => x.Item2).Select(x => x.Item1).Freeze();
      closures = (json, childrenIds);
      _closures.TryAdd(id, closures);
    }

    return closures;
  }

  public void DecodeOrEnqueueChildren(string id)
  {
    if (_baseCache.ContainsKey(id))
    {
      return;
    }
    (string json, IReadOnlyCollection<string> closures) = GetClosures(id);
    var @base = Deserialise(id, json, closures);
    _baseCache.TryAdd(id, @base);
    //remove from JSON cache because we've finally made the Base
    _closures.TryRemove(id, out _);
    _activeTasks.TryRemove(id, out _);
  }

  private Base Deserialise(string id, string json, IReadOnlyCollection<string> closures)
  {
    if (_baseCache.TryGetValue(id, out var baseObject))
    {
      return baseObject;
    }

    var deserializer = objectDeserializerFactory.Create(id, closures, _baseCache);
    return deserializer.Deserialize(json);
  }
}
