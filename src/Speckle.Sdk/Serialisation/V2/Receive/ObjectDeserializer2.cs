#if !NETSTANDARD2_0
using System.Text;
using System.Text.Json;
using Speckle.Sdk.Common;
using Speckle.Sdk.Dependencies;
using Speckle.Sdk.Models;

namespace Speckle.Sdk.Serialisation.V2.Receive;

public sealed class ObjectDeserializer2(
  IReadOnlyDictionary<string, Base> references,
  DeserializeOptions? options = null
) : IObjectDeserializer
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
    var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(objectJson));

    Base? converted;
    try
    {
      reader.Read();
      converted = (Base)ReadObject(reader).NotNull();
    }
    catch (Exception ex) when (!ex.IsFatal() && ex is not OperationCanceledException)
    {
      throw new SpeckleDeserializeException("Failed to deserialize", ex);
    }

    return converted;
  }

  private List<object?> ReadArrayAsync(Utf8JsonReader reader)
  {
    reader.Read();
    List<object?> retList = new();
    while (reader.TokenType != JsonTokenType.EndArray)
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

  private object? ReadObject(Utf8JsonReader reader)
  {
    reader.Read();
    Dictionary<string, object?> dict = Pools.ObjectDictionaries.Get();
    while (reader.TokenType != JsonTokenType.EndObject)
    {
      switch (reader.TokenType)
      {
        case JsonTokenType.PropertyName:
        {
          var propName = reader.GetString().NotNull();
            reader.Read(); //goes prop value
            object? convertedValue = ReadProperty(reader);
            dict[propName] = convertedValue;
            reader.Read(); //goes to next
          }
          break;
        default:
          throw new InvalidOperationException($"Unknown {reader.TokenType} with {reader.GetString()}");
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
      //since we don't throw on missing references, return null
      return null;
    }

    var b = DictionaryConverter.Dict2Base(dict, options?.SkipInvalidConverts ?? false);
    Pools.ObjectDictionaries.Return(dict);
    return b;
  }

  private object? ReadProperty(Utf8JsonReader reader)
  {
    switch (reader.TokenType)
    {
      case JsonTokenType.Null:
      case JsonTokenType.None:
        return null;
      case JsonTokenType.True:
      case JsonTokenType.False:
        return reader.GetBoolean();
      case JsonTokenType.Number:
        if (reader.TryGetInt64(out var longValue))
        {
          return longValue;
        }
        if (reader.TryGetDouble(out double d))
        {
          // This is behaviour carried over from v2 to facilitate large numbers from Python
          // This is quite hacky, as it's a bit questionable exactly what numbers are supported, and with what tolerance
          // For this reason, this can be considered undocumented behaviour, and is only for values within the range of a 64bit integer.
          return d;
        }

        throw new ArgumentException(
          $"Found an unsupported integer type {reader.TokenType} with value {reader.GetString()}"
        );
      case JsonTokenType.String:
        if (reader.TryGetDateTime(out var dateTime))
        {
          return dateTime;
        }
        return reader.GetString();
      case JsonTokenType.StartArray:
        return ReadArrayAsync(reader);
      case JsonTokenType.StartObject:
        var dict = ReadObject(reader);
        return dict;

      default:
        throw new ArgumentException("Json value not supported: " + reader.TokenType);
    }
  }
}
#endif
