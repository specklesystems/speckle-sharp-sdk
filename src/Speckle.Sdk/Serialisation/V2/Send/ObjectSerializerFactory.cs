using Speckle.InterfaceGenerator;

namespace Speckle.Sdk.Serialisation.V2.Send;

[GenerateAutoInterface]
public class ObjectSerializerFactory(IBasePropertyGatherer propertyGatherer) : IObjectSerializerFactory
{
  public IObjectSerializer Create(IReadOnlyDictionary<Id, NodeInfo> baseCache, CancellationToken cancellationToken) =>
    new ObjectSerializer(propertyGatherer, baseCache, true, cancellationToken);
}
