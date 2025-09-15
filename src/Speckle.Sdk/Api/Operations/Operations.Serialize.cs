using System.Collections.Concurrent;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation;

namespace Speckle.Sdk.Api;

public partial class Operations
{
  private static readonly Id s_emptyId = new(Guid.NewGuid().ToString());

  public string Serialize(Base value, CancellationToken cancellationToken = default)
  {
    using var serializer2 = objectSerializerFactory.Create(cancellationToken);
    var items = serializer2.Serialize(value);
    return items.First().Item2.Value;
  }

  public Task<Base> DeserializeAsync(string value, CancellationToken cancellationToken = default)
  {
    var deserializer = objectDeserializerFactory.Create(
      s_emptyId,
      new List<Id>(),
      new ConcurrentDictionary<Id, Base>()
    );
    return Task.FromResult(deserializer.Deserialize(new(value), cancellationToken));
  }
}
