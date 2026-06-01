using System.Drawing;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Speckle.Sdk.Pipelines.Receive.JsonConverters;

public sealed class ColorArgbConverter : JsonConverter<Color>
{
  public override Color Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
  {
    if (reader.TokenType != JsonTokenType.Number)
    {
      throw new JsonException($"Expected Color JSON value to be ARGB integer, got {reader.TokenType} instead");
    }

    // Signed 32-bit ARGB encoding: [-2,147,483,648, 2,147,483,647]
    if (reader.TryGetInt32(out int signedArgb))
    {
      return Color.FromArgb(signedArgb);
    }

    // Unsigned 32-bit ARGB encoding: [2,147,483,648, 4,294,967,295]
    // To support python's serializer which serializes colours this way.
    if (reader.TryGetUInt32(out uint unsignedArgb))
    {
      return Color.FromArgb(unchecked((int)unsignedArgb));
    }

    throw new JsonException("Color value is not a valid 32-bit ARGB integer");
  }

  public override void Write(Utf8JsonWriter writer, Color value, JsonSerializerOptions options)
  {
    writer.WriteNumberValue(value.ToArgb());
  }
}
