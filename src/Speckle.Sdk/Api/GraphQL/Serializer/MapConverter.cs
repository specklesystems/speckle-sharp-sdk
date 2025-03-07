using System.Diagnostics.CodeAnalysis;
using GraphQL;
using Speckle.Newtonsoft.Json;
using Speckle.Newtonsoft.Json.Linq;

namespace Speckle.Sdk.Api.GraphQL.Serializer;

internal sealed class MapConverter : JsonConverter<Map>
{
  public override void WriteJson(JsonWriter writer, Map? value, JsonSerializer serializer)
  {
    throw new NotImplementedException(
      "This converter currently is only intended to be used to read a JSON object into a strongly-typed representation."
    );
  }

  public override Map ReadJson(
    JsonReader reader,
    Type objectType,
    Map? existingValue,
    bool hasExistingValue,
    JsonSerializer serializer
  )
  {
    var rootToken = JToken.ReadFrom(reader);
    if (rootToken is JObject)
    {
      return (Map)ReadDictionary(rootToken, new Map());
    }

    throw new ArgumentException("This converter can only parse when the root element is a JSON Object.");
  }

  [SuppressMessage(
    "Maintainability",
    "CA1508:Avoid dead conditional code",
    Justification = "False positive, see https://github.com/dotnet/roslyn-analyzers/issues/6893"
  )]
  private object ReadToken(JToken token)
  {
    switch (token)
    {
      case JObject jObject:
        return ReadDictionary(jObject, new Dictionary<string, object>());
      case JArray jArray:
        return ReadArray(jArray).ToList();
      case JValue jValue:
        return jValue.Value ?? string.Empty;
      case JConstructor:
        throw new ArgumentOutOfRangeException(nameof(token), "cannot deserialize a JSON constructor");
      case JProperty:
        throw new ArgumentOutOfRangeException(nameof(token), "cannot deserialize a JSON property");
      case JContainer:
        throw new ArgumentOutOfRangeException(nameof(token), "cannot deserialize a JSON comment");
      default:
        throw new ArgumentOutOfRangeException(nameof(token), $"Invalid token type {token?.Type}");
    }
  }

  private Dictionary<string, object> ReadDictionary(JToken element, Dictionary<string, object> to)
  {
    foreach (var property in ((JObject)element).Properties())
    {
      if (IsUnsupportedJTokenType(property.Value.Type))
      {
        continue;
      }

      to[property.Name] = ReadToken(property.Value);
    }
    return to;
  }

  private IEnumerable<object> ReadArray(JArray element)
  {
    foreach (var item in element)
    {
      if (IsUnsupportedJTokenType(item.Type))
      {
        continue;
      }

      yield return ReadToken(item);
    }
  }

  private bool IsUnsupportedJTokenType(JTokenType type)
  {
    return type == JTokenType.Constructor || type == JTokenType.Property || type == JTokenType.Comment;
  }
}
