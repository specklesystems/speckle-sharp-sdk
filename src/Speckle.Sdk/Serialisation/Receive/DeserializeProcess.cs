using System.Collections.Concurrent;
using Speckle.Sdk.Common;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation.Utilities;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Serialisation.Receive;

public sealed class DeserializeProcess : IDisposable
{
  private readonly StackChannel<string> _deserializationStack = new();
  
  private readonly ConcurrentDictionary<string, Base> _cache = new();
  private readonly ConcurrentDictionary<string, IReadOnlyList<string>> _closures = new();
  private readonly ITransport _readTransport;

  public DeserializeProcess(ITransport readTransport)
  {
    _readTransport = readTransport;
  }
  public async Task<Base> Deserialize(string id)
  {
    _deserializationStack.Write(id);
    _deserializationStack.Start(async x =>
    {
      var json = await _readTransport.GetObject(x).ConfigureAwait(false);
      Execute(x, json.NotNull());
      return _cache.ContainsKey(id);
    });
    await _deserializationStack.CompleteAndWaitForReader().ConfigureAwait(false);
    return _cache[id];
  }
  
  public void Execute(string id, string json)
  {
    if (!_closures.TryGetValue(id, out var closures))
    {
      closures = ClosureParser.GetChildrenIds(json).ToList();
      _closures.TryAdd(id, closures);
    }

    List<string> notFoundIds = new();
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
  }

  private Base Deserialise(string id, string json)
  {
    if (_cache.TryGetValue(id, out var baseObject))
    {
      return baseObject;
    }
    SpeckleObjectDeserializer2 deserializer =
      new(_cache, SpeckleObjectSerializerPool.Instance);
    return deserializer.Deserialize(json);
  }

  public void Dispose() => _deserializationStack.Dispose();
}
