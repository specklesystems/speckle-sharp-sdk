using System.Collections.Concurrent;
using System.Threading.Channels;
using Speckle.Sdk.Common;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation.Utilities;

namespace Speckle.Sdk.Serialisation.Receive;

public record Deserialized(string Id, string Json, Base BaseObject);

public class DeserializeStage
{
  private readonly ConcurrentDictionary<string, IReadOnlyList<string>> _closures = new();

  public CachingStage? CachingStage { get; set; }
  public Channel<string> SourceChannel { get; set; }

  public long Deserialized { get; private set; }

  public async ValueTask<Deserialized?> Execute(Transported message)
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
      if (CachingStage.NotNull().Cache.TryGetValue(c, out var cached))
      {
        closureBases.Add(c, cached);
      }
      else
      {
        await SourceChannel.Writer.WriteAsync(c).ConfigureAwait(false);
        anyNotFound = true;
      }
    }

    if (anyNotFound)
    {
      await SourceChannel.Writer.WriteAsync(message.Id).ConfigureAwait(false);
      return null;
    }

    var @base = await Deserialise(closureBases, message.Id, message.Json).ConfigureAwait(false);
    _closures.TryRemove(message.Id, out _);
    Deserialized++;
    return new(message.Id, message.Json, @base);
  }

  private async ValueTask<Base> Deserialise(IReadOnlyDictionary<string, Base> dictionary, string id, string json)
  {
    if (CachingStage?.Cache.TryGetValue(id, out var baseObject) ?? false)
    {
      return baseObject;
    }
    SpeckleObjectDeserializer2 deserializer = new(dictionary);
    return await deserializer.DeserializeJsonAsync(json).ConfigureAwait(false);
  }
}
