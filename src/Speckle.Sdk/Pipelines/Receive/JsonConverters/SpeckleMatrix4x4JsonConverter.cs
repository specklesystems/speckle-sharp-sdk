using System.Text.Json;
using System.Text.Json.Serialization;
using Speckle.DoubleNumerics;

namespace Speckle.Sdk.Pipelines.Receive.JsonConverters;

public sealed class SpeckleMatrix4x4JsonConverter : JsonConverter<Matrix4x4>
{
  public override Matrix4x4 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
  {
    if (reader.TokenType != JsonTokenType.StartArray)
    {
      throw new JsonException("Expected start of array.");
    }

    double[] values = new double[16];

    for (int i = 0; i < 16; i++)
    {
      if (!reader.Read())
      {
        throw new JsonException("Unexpected end of JSON.");
      }

      if (reader.TokenType != JsonTokenType.Number)
      {
        throw new JsonException($"Expected number at index {i}.");
      }

      values[i] = reader.GetDouble();
    }

    if (!reader.Read() || reader.TokenType != JsonTokenType.EndArray)
    {
      throw new JsonException("Expected end of array.");
    }

    return new Matrix4x4(
      values[0],
      values[1],
      values[2],
      values[3],
      values[4],
      values[5],
      values[6],
      values[7],
      values[8],
      values[9],
      values[10],
      values[11],
      values[12],
      values[13],
      values[14],
      values[15]
    );
  }

  public override void Write(Utf8JsonWriter writer, Matrix4x4 value, JsonSerializerOptions options)
  {
    writer.WriteStartArray();

    writer.WriteNumberValue(value.M11);
    writer.WriteNumberValue(value.M12);
    writer.WriteNumberValue(value.M13);
    writer.WriteNumberValue(value.M14);

    writer.WriteNumberValue(value.M21);
    writer.WriteNumberValue(value.M22);
    writer.WriteNumberValue(value.M23);
    writer.WriteNumberValue(value.M24);

    writer.WriteNumberValue(value.M31);
    writer.WriteNumberValue(value.M32);
    writer.WriteNumberValue(value.M33);
    writer.WriteNumberValue(value.M34);

    writer.WriteNumberValue(value.M41);
    writer.WriteNumberValue(value.M42);
    writer.WriteNumberValue(value.M43);
    writer.WriteNumberValue(value.M44);

    writer.WriteEndArray();
  }
}
