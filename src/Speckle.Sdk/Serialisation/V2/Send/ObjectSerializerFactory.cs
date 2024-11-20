using System.Collections.Concurrent;
using Speckle.InterfaceGenerator;
using Speckle.Sdk.Models;

namespace Speckle.Sdk.Serialisation.V2.Send;

[GenerateAutoInterface]
public class ObjectSerializerFactory(IBasePropertyGatherer propertyGatherer) : IObjectSerializerFactory
{
  public IObjectSerializer Create(
    ConcurrentDictionary<Base, CacheInfo> baseCache,
    CancellationToken cancellationToken
  ) => new ObjectSerializer(propertyGatherer, baseCache, true, cancellationToken);
}
