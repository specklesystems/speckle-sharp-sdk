using System.Collections.Concurrent;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation.Utilities;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Serialisation.V2.Receive;

public record DeserializeOptions(bool? SkipCacheCheck = null);

public sealed class DeserializeProcess(IProgress<ProgressArgs>? progress, IObjectLoader objectLoader) : IDisposable
{
  private readonly StackChannel<string> _deserializationStack = new();

  private readonly ConcurrentDictionary<string, Base> _cache = new();
  private readonly ConcurrentDictionary<string, string> _loaded = new();
  private readonly ConcurrentDictionary<string, IReadOnlyList<string>> _closures = new();

  private long _total;

  public async Task<Base> Deserialize(
    string rootId,
    CancellationToken cancellationToken,
    DeserializeOptions? options = null
  )
  {
    var (rootJson, childrenIds) = await objectLoader
      .GetAndCache(rootId, cancellationToken, options)
      .ConfigureAwait(false);
    _total = childrenIds.Count;
    _loaded.TryAdd(rootId, rootJson);
    _closures.TryAdd(rootId, childrenIds);
    DecodeOrEnqueueChildren(rootId, rootJson);
    progress?.Report(new(ProgressEvent.DeserializeObject, _cache.Count, childrenIds.Count));
    await Traverse(rootId, cancellationToken).ConfigureAwait(false);
    return _cache[rootId];
  }

  public async Task Traverse(string id, CancellationToken cancellationToken)
  {
    if (_cache.ContainsKey(id))
    {
      return;
    }
    var json = GetJson(id);
    var tasks = new List<Task>();
    foreach (var childId in GetChildrenIds(id, json))
    {
      if (_cache.ContainsKey(childId))
      {
        continue;
      }
      // tmp is necessary because of the way closures close over loop variables
      var tmpId = childId;
      Task<Task> t = Task.Factory.StartNew(
        () => Traverse(tmpId, cancellationToken),
        cancellationToken,
        TaskCreationOptions.AttachedToParent,
        TaskScheduler.Default
      );
      tasks.Add(t.Unwrap());
    }

    if (tasks.Count > 0)
    {
      await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    //don't redo things if the id is decoded already in the cache
    if (!_cache.ContainsKey(id))
    {
      DecodeOrEnqueueChildren(id, json);
      progress?.Report(new(ProgressEvent.DeserializeObject, _cache.Count, _total));
    }
  }

  private IReadOnlyList<string> GetClosures(string id, string json)
  {
    if (!_closures.TryGetValue(id, out var closures))
    {
      closures = ClosureParser.GetClosures(json).OrderByDescending(x => x.Item2).Select(x => x.Item1).ToList();
      _closures.TryAdd(id, closures);
    }

    return closures;
  }

  public IEnumerable<string> GetChildrenIds(string id, string json)
  {
    var closures = GetClosures(id, json);
    return closures;
  }

  public string GetJson(string id)
  {
    if (!_loaded.TryGetValue(id, out var json))
    {
      var j = objectLoader.LoadId(id);
      if (j == null)
      {
        throw new InvalidOperationException();
      }

      json = j;
      _loaded.TryAdd(id, json);
    }

    return json;
  }

  public void DecodeOrEnqueueChildren(string id, string json)
  {
    if (_cache.ContainsKey(id))
    {
      return;
    }
    var closures = GetClosures(id, json);

    List<string> notFoundIds = SpeckleObjectSerializerPool.Instance.ListString.Get();
    foreach (var closureId in closures)
    {
      if (!_cache.ContainsKey(closureId))
      {
        notFoundIds.Add(closureId);
      }
    }

    if (notFoundIds.Count > 0)
    {
      notFoundIds.Add(id);
      notFoundIds.Reverse();
      _deserializationStack.Write(notFoundIds.ToArray());
    }
    else if (!_cache.ContainsKey(id))
    {
      var @base = Deserialise(id, json);
      _closures.TryRemove(id, out _);
      _cache.TryAdd(id, @base);
    }
    SpeckleObjectSerializerPool.Instance.ListString.Return(notFoundIds);
  }

  private Base Deserialise(string id, string json)
  {
    if (_cache.TryGetValue(id, out var baseObject))
    {
      return baseObject;
    }
    SpeckleObjectDeserializer2 deserializer = new(_cache, SpeckleObjectSerializerPool.Instance);
    return deserializer.Deserialize(json);
  }

  public void Dispose() => _deserializationStack.Dispose();
}
