using Speckle.InterfaceGenerator;
using Speckle.Sdk.Dependencies;
using Speckle.Sdk.Models;
using Closures = System.Collections.Generic.Dictionary<Speckle.Sdk.Serialisation.Id, int>;

namespace Speckle.Sdk.Serialisation.V2.Send;

[GenerateAutoInterface]
public class ObjectSerializerFactory(IBasePropertyGatherer propertyGatherer) : IObjectSerializerFactory
{
  private readonly Pool<List<(Id, Json, Closures)>> _chunkPool = Pools.CreateListPool<(Id, Json, Closures)>();
  private readonly Pool<List<DataChunk>> _chunk2Pool = Pools.CreateListPool<DataChunk>();
  private readonly Pool<List<object?>> _chunk3Pool = Pools.CreateListPool<object?>();

  public IObjectSerializer Create(IReadOnlyDictionary<Id, NodeInfo> baseCache, CancellationToken cancellationToken) =>
    new ObjectSerializer(propertyGatherer, baseCache, _chunkPool, _chunk2Pool, _chunk3Pool, cancellationToken);
}
