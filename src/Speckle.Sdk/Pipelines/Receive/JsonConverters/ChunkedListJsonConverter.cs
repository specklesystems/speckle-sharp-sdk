using System.Text.Json;
using System.Text.Json.Serialization;
using Speckle.Sdk.Common;
using Speckle.Sdk.Models;

namespace Speckle.Sdk.Pipelines.Receive.JsonConverters;

public abstract class ChunkedListJsonConverter<T>(PackFileManager packFileManager) : JsonConverter<List<T>>
{
  internal virtual LightWeightDataChunk<T> DereferenceDetachedReference(ref Utf8JsonReader reader)
  {
    var reference = JsonSerializer.Deserialize<LightWeightObjectReference>(ref reader).NotNull();
    string chunkJson = packFileManager.GetObjectString(reference.referencedId);
    var dataChunk = JsonSerializer.Deserialize<LightWeightDataChunk<T>>(chunkJson).NotNull();
    return dataChunk;
  }

  public override List<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
  {
    var list = new List<T>();

    while (reader.Read())
    {
      switch (reader.TokenType)
      {
        case JsonTokenType.EndArray:
          return list;
        case JsonTokenType.StartObject:
        {
          list.AddRange(DereferenceDetachedReference(ref reader).data);
          break;
        }
        default:
          JsonSerializer.Deserialize<double>(ref reader, options);
          break;
      }
    }

    throw new JsonException("Unexpected end of JSON array.");
  }

  public override void Write(Utf8JsonWriter writer, List<T> value, JsonSerializerOptions options) =>
    throw new NotSupportedException();
}

public sealed class ChunkedDoubleListJsonConverter(PackFileManager packFileManager)
  : ChunkedListJsonConverter<double>(packFileManager)
{
#if NET6_0_OR_GREATER
  private readonly PackFileManager _packFileManager = packFileManager;

  internal override LightWeightDataChunk<double> DereferenceDetachedReference(ref Utf8JsonReader reader)
  {
    var reference = JsonSerializer
      .Deserialize(ref reader, SpeckleJsonContext.Default.LightWeightObjectReference)
      .NotNull();
    string chunkJson = _packFileManager.GetObjectString(reference.referencedId);
    var dataChunk = JsonSerializer
      .Deserialize(chunkJson, SpeckleJsonContext.Default.LightWeightDataChunkDouble)
      .NotNull();
    return dataChunk;
  }
#endif
}

public sealed class ChunkedInt32ListJsonConverter(PackFileManager packFileManager)
  : ChunkedListJsonConverter<int>(packFileManager)
{
#if NET6_0_OR_GREATER
  private readonly PackFileManager _packFileManager = packFileManager;

  internal override LightWeightDataChunk<int> DereferenceDetachedReference(ref Utf8JsonReader reader)
  {
    var reference = JsonSerializer
      .Deserialize(ref reader, SpeckleJsonContext.Default.LightWeightObjectReference)
      .NotNull();
    string chunkJson = _packFileManager.GetObjectString(reference.referencedId);
    var dataChunk = JsonSerializer
      .Deserialize(chunkJson, SpeckleJsonContext.Default.LightWeightDataChunkInt32)
      .NotNull();
    return dataChunk;
  }
#endif
}
