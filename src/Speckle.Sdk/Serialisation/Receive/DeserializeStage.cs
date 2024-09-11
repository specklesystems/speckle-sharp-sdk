using System.Collections.Concurrent;
using Speckle.Sdk.Models;

namespace Speckle.Sdk.Serialisation.Receive;

public record Deserialized(string Id, Base BaseObject);

public class DeserializeStage
{
  private readonly ConcurrentDictionary<string, Base> _cache;

  public DeserializeStage(ConcurrentDictionary<string, Base> cache)
  {
    _cache = cache;
  }

  public long Deserialized { get; private set; }

  public async ValueTask<Deserialized?> Execute(Downloaded message)
  {
    SpeckleObjectDeserializer2 deserializer = new(_cache);
    var @base =  await deserializer.DeserializeJsonAsync(message.Json).ConfigureAwait(false);
    Deserialized++;
    return new(message.Id, @base);
  }
}
