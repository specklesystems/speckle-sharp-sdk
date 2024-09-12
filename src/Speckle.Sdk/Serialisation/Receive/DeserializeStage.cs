using System.Collections.Concurrent;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation.Utilities;

namespace Speckle.Sdk.Serialisation.Receive;

public record Deserialized(string Id, Base BaseObject);

public class DeserializeStage(ConcurrentDictionary<string, Base> cache, Func<string, ValueTask> gatherId)
{
  private readonly ConcurrentDictionary<string, IReadOnlyList<string>> _closures = new();

  public long Deserialized { get; private set; }

  public async ValueTask<Deserialized?> Execute(Downloaded message)
  {
    if (!_closures.TryGetValue(message.Id, out var closures))
    {
      closures = (await ClosureParser.GetChildrenIdsAsync(message.Json).ConfigureAwait(false)).ToList();
      _closures.TryAdd(message.Id, closures);
    }

    var closureBases = new Dictionary<string, Base>();
    bool anyNotFound = false;
    foreach (var c in closures)
    {
      if (cache.TryGetValue(c, out var cached))
      {
        closureBases.Add(c, cached);
      }
      else
      {
        await gatherId(c).ConfigureAwait(false);
        anyNotFound = true;
      }
    }

    if (anyNotFound)
    {
      await gatherId(message.Id).ConfigureAwait(false);
      return null;
    }

    var @base = await Deserialise(closureBases, message.Id, message.Json).ConfigureAwait(false);
    _closures.TryRemove(message.Id, out _);
    Deserialized++;
    return new(message.Id,  @base);
  }

  private async ValueTask<Base> Deserialise(IReadOnlyDictionary<string, Base> dictionary, string id, string json)
  {
    if (cache.TryGetValue(id, out var baseObject))
    {
      return baseObject;
    }
    SpeckleObjectDeserializer2 deserializer = new(dictionary);
    return await deserializer.DeserializeJsonAsync(json).ConfigureAwait(false);
  }
}
