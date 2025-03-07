using System.Collections.Concurrent;
using Speckle.InterfaceGenerator;
using Speckle.Sdk.Models;

namespace Speckle.Sdk.Serialisation.V2.Receive;

[GenerateAutoInterface]
public class BaseDeserializer(IObjectDeserializerFactory objectDeserializerFactory) : IBaseDeserializer
{
  public Base Deserialise(
    ConcurrentDictionary<Id, Base> baseCache,
    Id id,
    Json json,
    IReadOnlyCollection<Id> closures,
    CancellationToken cancellationToken
  )
  {
    if (baseCache.TryGetValue(id, out var baseObject))
    {
      return baseObject;
    }

    var deserializer = objectDeserializerFactory.Create(id, closures, baseCache);
    return deserializer.Deserialize(json, cancellationToken);
  }
}
