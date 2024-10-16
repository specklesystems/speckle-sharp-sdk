using System.Collections.Concurrent;
using Speckle.Sdk.Common;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation.Utilities;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Serialisation.Receive;

public sealed class DeserializeProcess(IProgress<ProgressArgs>? progress, ObjectLoader objectLoader, SQLiteTransport sqLiteTransport) : IDisposable
{
  private readonly StackChannel<string> _deserializationStack = new();

  private readonly ConcurrentDictionary<string, Base> _cache = new();
  private readonly ConcurrentDictionary<string, IReadOnlyList<string>> _closures = new();

  public async Task<Base> Deserialize(string rootId)
  {
    var (rootJson, childrenIds) = 
      await objectLoader.DownloadAndLoad(rootId, default).ConfigureAwait(false);
    Execute(rootId, rootJson);
    var count = 0L;
    progress?.Report(new (ProgressEvent.DeserializeObject, count, childrenIds.Count));
    _deserializationStack.Write(childrenIds.ToArray());
    _deserializationStack.Start(async id =>
    {
      if (_cache.ContainsKey(id))
      {
        return id == rootId;
      }
      var json = await sqLiteTransport.GetObject(id).ConfigureAwait(false);
      Execute(id, json.NotNull());
      count++;
      progress?.Report(new (ProgressEvent.DeserializeObject, count, childrenIds.Count));
      return _cache.ContainsKey(rootId);
    });
    await _deserializationStack.CompleteAndWaitForReader().ConfigureAwait(false);
    return _cache[rootId];
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
