using Speckle.InterfaceGenerator;
using Speckle.Sdk.Models;

namespace Speckle.Sdk.Serialisation.V2.Receive;

[GenerateAutoInterface]
public class ObjectDeserializerFactory : IObjectDeserializerFactory
{
  public IObjectDeserializer Create(IReadOnlyDictionary<string, Base> references, DeserializeOptions? options = null) =>
    new ObjectDeserializer(references, SpeckleObjectSerializerPool.Instance, options);

#if !NETSTANDARD2_0
  public IObjectDeserializer Create2(
    IReadOnlyDictionary<string, Base> references,
    DeserializeOptions? options = null
  ) => new ObjectDeserializer2(references, options);
#endif
}
