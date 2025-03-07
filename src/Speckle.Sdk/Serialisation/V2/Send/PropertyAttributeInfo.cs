using Speckle.Newtonsoft.Json;

namespace Speckle.Sdk.Serialisation.V2.Send;

public readonly struct PropertyAttributeInfo
{
  public PropertyAttributeInfo(
    bool isDetachable,
    bool isChunkable,
    int chunkSize,
    JsonPropertyAttribute? jsonPropertyAttribute
  )
  {
    IsDetachable = isDetachable || isChunkable;
    IsChunkable = isChunkable;
    ChunkSize = chunkSize;
    JsonPropertyInfo = jsonPropertyAttribute;
  }

  public readonly bool IsDetachable;
  public readonly bool IsChunkable;
  public readonly int ChunkSize;
  public readonly JsonPropertyAttribute? JsonPropertyInfo;
}
