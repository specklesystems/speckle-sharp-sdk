using System.Collections.Concurrent;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation.Send;
using Speckle.Sdk.Serialisation.Utilities;

namespace Speckle.Sdk.Serialisation.Receive;

public record Deserialized(string Id, Base BaseObject);

public class DeserializeStage(
  ConcurrentDictionary<string, Base> cache,
  Func<string, CancellationToken, ValueTask> gatherId,
  Action<Deserialized> done,
  DeserializedOptions? deserializedOptions
)
{
  private readonly ConcurrentDictionary<string, IReadOnlyList<string>> _closures = new();

  public long Deserialized { get; private set; }

  public async ValueTask Execute(Downloaded message, CancellationToken cancellationToken)
  {
    if (!_closures.TryGetValue(message.Id, out var closures))
    {
      closures = ClosureParser.GetChildrenIds(message.Json).ToList();
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
        await gatherId(c, cancellationToken).ConfigureAwait(false);
        anyNotFound = true;
      }
    }

    if (anyNotFound)
    {
      await gatherId(message.Id, cancellationToken).ConfigureAwait(false);
    }
    else
    {
      var @base = Deserialise(closureBases, message.Id, message.Json);
      _closures.TryRemove(message.Id, out _);
      Deserialized++;
      done(new(message.Id, @base));
    }
  }

  private Base Deserialise(IReadOnlyDictionary<string, Base> dictionary, string id, string json)
  {
    if (cache.TryGetValue(id, out var baseObject))
    {
      return baseObject;
    }
    SpeckleObjectDeserializer2 deserializer =
      new(dictionary, SpeckleObjectSerializer2Pool.Instance, deserializedOptions);
    return deserializer.Deserialize(json);
  }
}
