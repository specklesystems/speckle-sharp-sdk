using Speckle.InterfaceGenerator;
using Speckle.Sdk.Models;

namespace Speckle.Sdk.Serialisation.V2.Receive;

[GenerateAutoInterface]
public class ObjectDeserializerFactory : IObjectDeserializerFactory
{
  public IObjectDeserializer Create(
    Id currentId,
    IReadOnlyCollection<Id> currentClosures,
    IReadOnlyDictionary<Id, Base> references,
    DeserializeProcessOptions? options = null
  ) => new ObjectDeserializer(currentId, currentClosures, references, SpeckleObjectSerializerPool.Instance, options);
}
