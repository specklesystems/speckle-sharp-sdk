using Speckle.Newtonsoft.Json;

namespace Speckle.Sdk.Serialisation.V2.Send;

public readonly struct PropertyAttributeInfo(
  bool isDetachable,
  bool isChunkable,
  int chunkSize,
  JsonPropertyAttribute? jsonPropertyAttribute
)
{
  public readonly bool IsDetachable = isDetachable || isChunkable;
  public readonly bool IsChunkable = isChunkable;
  public readonly int ChunkSize = chunkSize;
  public readonly JsonPropertyAttribute? JsonPropertyInfo = jsonPropertyAttribute;
}
