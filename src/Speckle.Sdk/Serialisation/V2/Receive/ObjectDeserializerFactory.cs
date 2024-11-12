using Speckle.InterfaceGenerator;
using Speckle.Sdk.Models;

namespace Speckle.Sdk.Serialisation.V2.Receive;

[GenerateAutoInterface]
public class ObjectDeserializerFactory : IObjectDeserializerFactory
{
  public IObjectDeserializer Create(IReadOnlyDictionary<string, Base> references, DeserializeOptions? options = null) =>
    new ObjectDeserializer(references, SpeckleObjectSerializerPool.Instance, options);
}
