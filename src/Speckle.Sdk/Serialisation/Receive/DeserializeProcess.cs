using System.Collections.Concurrent;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation.Utilities;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Serialisation.Receive;

public sealed class DeserializeProcess(
  IProgress<ProgressArgs>? progress,
  ObjectLoader objectLoader,
  SQLiteTransport sqLiteTransport
) : IDisposable
{
  private readonly StackChannel<string> _deserializationStack = new();

  private readonly ConcurrentDictionary<string, Base> _cache = new();
  private readonly ConcurrentDictionary<string, IReadOnlyList<string>> _closures = new();

  private long _total;

  public async Task<Base> Deserialize(string rootId)
  {
    var (rootJson, childrenIds) = await objectLoader.DownloadAndLoad(rootId, default).ConfigureAwait(false);
    _total = childrenIds.Count;
    _closures.TryAdd(rootId, childrenIds);
    Execute(rootId, rootJson);
    progress?.Report(new(ProgressEvent.DeserializeObject, _cache.Count, childrenIds.Count));
    Traverse(rootId, rootJson);
    return _cache[rootId];
  }
  
  public void Traverse(string id, string json)
  {
    var tasks = new List<Task>();
    foreach (var (childId, childJson) in GetChildrenIds(id, json))
    {
      // tmp is necessary because of the way closures close over loop variables
      var tmpId = childId;
      var tmpJson = childJson;
#pragma warning disable CA2008
      tasks.Add(Task.Factory.StartNew(() => Traverse(tmpId, tmpJson)));
#pragma warning restore CA2008
    }

    if (tasks.Count > 0)
    {
      Task.WaitAll(tasks.ToArray());
    }

    Execute(id, json);
    progress?.Report(new(ProgressEvent.DeserializeObject, _cache.Count, _total));
  }

  public IEnumerable<(string, string)> GetChildrenIds(string id, string json)
  {
    if (!_closures.TryGetValue(id, out var closures))
    {
      closures = ClosureParser.GetClosures(json).OrderByDescending(x => x.Item2).Select(x => x.Item1).ToList();
      _closures.TryAdd(id, closures);
    }
    
    return sqLiteTransport.GetObjects(closures);
  }
  public void Execute(string id, string json)
  {
    if (!_closures.TryGetValue(id, out var closures))
    {
      closures = ClosureParser.GetClosures(json).OrderByDescending(x => x.Item2).Select(x => x.Item1).ToList();
      _closures.TryAdd(id, closures);
    }

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
    else
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
