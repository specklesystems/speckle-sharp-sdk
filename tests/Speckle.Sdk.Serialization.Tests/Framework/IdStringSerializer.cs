using System.Diagnostics.CodeAnalysis;
using Argon;
using Speckle.Sdk.Common;
using Speckle.Sdk.Serialisation;

namespace Speckle.Sdk.Serialization.Tests.Framework;

[SuppressMessage("Design", "CA1062:Validate arguments of public methods")]
public class IdStringSerializer : JsonConverter
{
  public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
  {
    var id = (Id)value;
    writer.WriteRawValue(id.Value);
  }

  public override object? ReadJson(JsonReader reader, Type type, object? existingValue, JsonSerializer serializer)
  {
    var json = reader.ReadAsString();
    return new Id(json.NotNull());
  }

  public override bool CanConvert(Type type) => typeof(Id) == type;
}
