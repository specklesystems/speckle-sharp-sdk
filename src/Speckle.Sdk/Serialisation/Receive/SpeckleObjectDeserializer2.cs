using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Common;
using Speckle.Sdk.Logging;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation.Send;

namespace Speckle.Sdk.Serialisation.Receive;

public record DeserializedOptions(bool ThrowOnMissingReferences = true);

public sealed class SpeckleObjectDeserializer2(
  IReadOnlyDictionary<string, Base> references,
  SpeckleObjectSerializer2Pool pool,
  DeserializedOptions? options = null
)
{
  /// <param name="objectJson">The JSON string of the object to be deserialized <see cref="Base"/></param>
  /// <returns>A <see cref="Base"/> typed object deserialized from the <paramref name="objectJson"/></returns>
  /// <exception cref="ArgumentNullException"><paramref name="objectJson"/> was null</exception>
  /// <exception cref="SpeckleDeserializeException"><paramref name="objectJson"/> cannot be deserialised to type <see cref="Base"/></exception>
  // /// <exception cref="TransportException"><see cref="ReadTransport"/> did not contain the required json objects (closures)</exception>
  public Base Deserialize(string objectJson)
  {
    if (objectJson is null)
    {
      throw new ArgumentNullException(nameof(objectJson), $"Cannot deserialize {nameof(objectJson)}, value was null");
    }
    // Apparently this automatically parses DateTimes in strings if it matches the format:
    // JObject doc1 = JObject.Parse(objectJson);

    // This is equivalent code that doesn't parse datetimes:
    using var stringReader = new StringReader(objectJson);
    using JsonTextReader reader = pool.GetJsonTextReader(stringReader);

    reader.DateParseHandling = DateParseHandling.None;

    Base? converted;
    try
    {
      reader.Read();
      converted = (Base)ReadObject(reader);
    }
    catch (Exception ex) when (!ex.IsFatal() && ex is not OperationCanceledException)
    {
      throw new SpeckleDeserializeException("Failed to deserialize", ex);
    }

    return converted;
  }

  private List<object?> ReadArrayAsync(JsonReader reader)
  {
    reader.Read();
    List<object?> retList = new();
    while (reader.TokenType != JsonToken.EndArray)
    {
      object? convertedValue = ReadProperty(reader);
      if (convertedValue is DataChunk chunk)
      {
        retList.AddRange(chunk.data);
      }
      else
      {
        retList.Add(convertedValue);
      }
      reader.Read(); //goes to next
    }
    return retList;
  }

  private object ReadObject(JsonReader reader)
  {
    reader.Read();
    Dictionary<string, object?> dict = new();
    while (reader.TokenType != JsonToken.EndObject)
    {
      switch (reader.TokenType)
      {
        case JsonToken.PropertyName:
          {
            var propName = reader.Value.NotNull().ToString().NotNull();
            reader.Read(); //goes prop value
            object? convertedValue = ReadProperty(reader);
            dict[propName] = convertedValue;
            reader.Read(); //goes to next
          }
          break;
        default:
          throw new InvalidOperationException($"Unknown {reader.ValueType} with {reader.Value}");
      }
    }

    if (!dict.TryGetValue(DictionaryConverter.TYPE_DISCRIMINATOR, out object? speckleType))
    {
      return dict;
    }

    if (speckleType as string == "reference" && dict.TryGetValue("referencedId", out object? referencedId))
    {
      var objId = (string)referencedId.NotNull();
      if (references.TryGetValue(objId, out Base? closure))
      {
        return closure;
      }

      if (options is null || options.ThrowOnMissingReferences)
      {
        throw new InvalidOperationException($"missing reference: {objId}");
      }
    }

    return DictionaryConverter.Dict2Base(dict);
  }

  private object? ReadProperty(JsonReader reader)
  {
    switch (reader.TokenType)
    {
      case JsonToken.Undefined:
      case JsonToken.Null:
      case JsonToken.None:
        return null;
      case JsonToken.Boolean:
        return (bool)reader.Value.NotNull();
      case JsonToken.Integer:
        try
        {
          return (long)reader.Value.NotNull();
        }
        catch (OverflowException ex)
        {
          var v = (object)(double)reader.Value.NotNull();
          SpeckleLog.Logger.Debug(
            ex,
            "Json property {tokenType} failed to deserialize {value} to {targetType}, will be deserialized as {fallbackType}",
            reader.ValueType,
            v,
            typeof(long),
            typeof(double)
          );
          return v;
        }
      case JsonToken.Float:
        return (double)reader.Value.NotNull();
      case JsonToken.String:
        return (string?)reader.Value.NotNull();
      case JsonToken.Date:
        return (DateTime)reader.Value.NotNull();
      case JsonToken.StartArray:
        return ReadArrayAsync(reader);
      case JsonToken.StartObject:
        var dict = ReadObject(reader);
        return dict;

      default:
        throw new ArgumentException("Json value not supported: " + reader.ValueType);
    }
  }
}
