using Speckle.Sdk.Dependencies;
using Speckle.Sdk.Models;

namespace Speckle.Sdk.Serialisation.V2.Send;

public static class SerializerPools
{
  public static readonly Pool<List<DataChunk>> DataChunkPool  = new();
  
  public static readonly Pool<List<(Id, Json)>> ChunkPool  = new();
  public static readonly Pool< HashSet<object>> ParentObjectsPool  = new();
  public static readonly Pool< Dictionary<Id, ObjectReference>> ObjectReferencesPool  = new();
}
