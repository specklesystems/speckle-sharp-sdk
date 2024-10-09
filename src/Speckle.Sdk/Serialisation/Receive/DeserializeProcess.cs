using System.Collections.Concurrent;
using Speckle.Sdk.Common;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation.Utilities;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Serialisation.Receive;

public sealed class DeserializeProcess(ITransport readTransport) : IDisposable
{
  private readonly StackChannel<string> _deserializationStack = new();

  private readonly ConcurrentDictionary<string, Base> _cache = new();
  private readonly ConcurrentDictionary<string, IReadOnlyList<string>> _closures = new();

  public async Task<Base> Deserialize(string rootId)
  {
    _deserializationStack.Write(rootId);
    _deserializationStack.Start(async id =>
    {
      if (_cache.ContainsKey(id))
      {
        return id == rootId;
      }
      var json = await readTransport.GetObject(id).ConfigureAwait(false);
      Execute(id, json.NotNull());
      return _cache.ContainsKey(rootId);
    });
    await _deserializationStack.CompleteAndWaitForReader().ConfigureAwait(false);
    return _cache[rootId];
  }

  public void Execute(string id, string json)
  {
    if (!_closures.TryGetValue(id, out var closures))
    {
      closures = ClosureParser.GetChildrenIds(json).ToList();
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
