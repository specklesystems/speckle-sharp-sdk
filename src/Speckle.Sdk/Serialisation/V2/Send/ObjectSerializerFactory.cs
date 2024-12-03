using Speckle.InterfaceGenerator;

namespace Speckle.Sdk.Serialisation.V2.Send;

[GenerateAutoInterface]
public class ObjectSerializerFactory(IBasePropertyGatherer propertyGatherer) : IObjectSerializerFactory
{
  public IObjectSerializer Create(CancellationToken cancellationToken) =>
    new ObjectSerializer(propertyGatherer, true, cancellationToken);
}
