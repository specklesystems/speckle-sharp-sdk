using System.Diagnostics.CodeAnalysis;
using Argon;
using Speckle.Sdk.Common;
using Speckle.Sdk.Serialisation;

namespace Speckle.Sdk.Testing.Framework;

[SuppressMessage("Design", "CA1062:Validate arguments of public methods")]
public class JsonStringSerializer : JsonConverter
{
  public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
  {
    var json = (Json)value;
    writer.WriteRawValue(JObject.Parse(json.Value).ToString(Formatting.Indented));
  }

  public override object? ReadJson(JsonReader reader, Type type, object? existingValue, JsonSerializer serializer)
  {
    var json = reader.ReadAsString();
    return new Json(json.NotNull());
  }

  public override bool CanConvert(Type type) => typeof(Json) == type;
}
