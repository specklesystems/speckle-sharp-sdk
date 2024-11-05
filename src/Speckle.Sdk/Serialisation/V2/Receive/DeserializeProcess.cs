using System.Collections.Concurrent;
using System.Data;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation.Utilities;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Serialisation.V2.Receive;

public record DeserializeOptions(bool SkipCache);

public sealed class DeserializeProcess(IProgress<ProgressArgs>? progress, IObjectLoader objectLoader)
{
  private readonly ConcurrentDictionary<string, (string, IReadOnlyList<string>)> _closures = new();
  private long _total;
  private DeserializeOptions _options = new(false);

  public ConcurrentDictionary<string, Base> BaseCache { get; } = new();
  private readonly ConcurrentDictionary<string, Task> _activeTasks = new();

  public async Task<Base> Deserialize(
    string rootId,
    CancellationToken cancellationToken,
    DeserializeOptions? options = null
  )
  {
    _options = options ?? _options;
    var (rootJson, childrenIds) = await objectLoader
      .GetAndCache(rootId, _options, cancellationToken)
      .ConfigureAwait(false);
    _total = childrenIds.Count;
    _closures.TryAdd(rootId, (rootJson, childrenIds));
    progress?.Report(new(ProgressEvent.DeserializeObject, BaseCache.Count, childrenIds.Count));
    await Traverse(rootId, cancellationToken).ConfigureAwait(false);
    return BaseCache[rootId];
  }

  private async Task Traverse(string id, CancellationToken cancellationToken)
  {
    if (BaseCache.ContainsKey(id))
    {
      return;
    }
    var (_, childIds) = GetClosures(id);
    var tasks = new List<Task>();
    foreach (var childId in childIds)
    {
      if (BaseCache.ContainsKey(childId))
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
    if (!BaseCache.ContainsKey(id))
    {
      DecodeOrEnqueueChildren(id);
      progress?.Report(new(ProgressEvent.DeserializeObject, BaseCache.Count, _total));
    }
  }

  private (string, IReadOnlyList<string>) GetClosures(string id)
  {
    if (!_closures.TryGetValue(id, out var closures))
    {
      var json = objectLoader.LoadId(id);
      if (json == null)
      {
        throw new MissingPrimaryKeyException($"Missing object id in SQLite cache: {id}");
      }
      var childrenIds = ClosureParser.GetClosures(json).OrderByDescending(x => x.Item2).Select(x => x.Item1).ToList();
      closures = (json, childrenIds);
      _closures.TryAdd(id, closures);
    }

    return closures;
  }

  public void DecodeOrEnqueueChildren(string id)
  {
    if (BaseCache.ContainsKey(id))
    {
      return;
    }
    (string json, _) = GetClosures(id);
    var @base = Deserialise(id, json);
    BaseCache.TryAdd(id, @base);
    //remove from JSON cache because we've finally made the Base
    _closures.TryRemove(id, out _);
    _activeTasks.TryRemove(id, out _);
  }

  private Base Deserialise(string id, string json)
  {
    if (BaseCache.TryGetValue(id, out var baseObject))
    {
      return baseObject;
    }
    SpeckleObjectDeserializer2 deserializer = new(BaseCache, SpeckleObjectSerializerPool.Instance);
    return deserializer.Deserialize(json);
  }
}
