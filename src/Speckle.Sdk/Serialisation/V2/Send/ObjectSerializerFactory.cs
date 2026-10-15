using Speckle.InterfaceGenerator;
using Speckle.Sdk.Dependencies;
using Speckle.Sdk.Models;
#pragma warning disable CS0618 // Type or member is obsolete
using Closures = System.Collections.Generic.Dictionary<Speckle.Sdk.Serialisation.Id, int>;
#pragma warning restore CS0618 // Type or member is obsolete

namespace Speckle.Sdk.Serialisation.V2.Send;

[GenerateAutoInterface]
[Obsolete(Json.DEPRECATION_MESSAGE)]
public class ObjectSerializerFactory(IBasePropertyGatherer propertyGatherer) : IObjectSerializerFactory
{
  private readonly Pool<List<(Id, Json, Closures)>> _chunkPool = Pools.CreateListPool<(Id, Json, Closures)>();
  private readonly Pool<List<DataChunk>> _chunk2Pool = Pools.CreateListPool<DataChunk>();
  private readonly Pool<List<object?>> _chunk3Pool = Pools.CreateListPool<object?>();

  public IObjectSerializer Create(CancellationToken cancellationToken) =>
    new ObjectSerializer(propertyGatherer, _chunkPool, _chunk2Pool, _chunk3Pool, cancellationToken);
}
