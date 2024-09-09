using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Common;
using Speckle.Sdk.Logging;
using Speckle.Sdk.Models;

namespace Speckle.Sdk.Serialisation.Receive;

public sealed class SpeckleObjectDeserializer2
{
  public CancellationToken CancellationToken { get; set; }

  private readonly IReadOnlyDictionary<string, Base> _closures;

  public SpeckleObjectDeserializer2(IReadOnlyDictionary<string, Base> closures)
  {
    _closures = closures;
  }

  /// <param name="objectJson">The JSON string of the object to be deserialized <see cref="Base"/></param>
  /// <returns>A <see cref="Base"/> typed object deserialized from the <paramref name="objectJson"/></returns>
  /// <exception cref="ArgumentNullException"><paramref name="objectJson"/> was null</exception>
  /// <exception cref="SpeckleDeserializeException"><paramref name="objectJson"/> cannot be deserialised to type <see cref="Base"/></exception>
  // /// <exception cref="TransportException"><see cref="ReadTransport"/> did not contain the required json objects (closures)</exception>
  public async ValueTask<Base> DeserializeJsonAsync(string objectJson)
  {
    if (objectJson is null)
    {
      throw new ArgumentNullException(nameof(objectJson), $"Cannot deserialize {nameof(objectJson)}, value was null");
    }
    // Apparently this automatically parses DateTimes in strings if it matches the format:
    // JObject doc1 = JObject.Parse(objectJson);

    // This is equivalent code that doesn't parse datetimes:
    using JsonReader reader = new JsonTextReader(new StringReader(objectJson));

    reader.DateParseHandling = DateParseHandling.None;

    Base? converted;
    try
    {
      await reader.ReadAsync(CancellationToken).ConfigureAwait(false);
      converted = (Base)await ReadObjectAsync(reader, CancellationToken).ConfigureAwait(false);
    }
    catch (Exception ex) when (!ex.IsFatal() && ex is not OperationCanceledException)
    {
      throw new SpeckleDeserializeException($"Failed to deserialize", ex);
    }

    return converted;
  }

  private async ValueTask<List<object?>> ReadArrayAsync(JsonReader reader, CancellationToken ct)
  {
    await reader.ReadAsync(ct).ConfigureAwait(false);
    List<object?> retList = new();
    while (reader.TokenType != JsonToken.EndArray)
    {
      object? convertedValue = await ReadPropertyAsync(reader, ct).ConfigureAwait(false);
      if (convertedValue is DataChunk chunk)
      {
        retList.AddRange(chunk.data);
      }
      else
      {
        retList.Add(convertedValue);
      }
      await reader.ReadAsync(ct).ConfigureAwait(false); //goes to next
    }
    return retList;
  }

  private async ValueTask<object> ReadObjectAsync(JsonReader reader, CancellationToken ct)
  {
    await reader.ReadAsync(ct).ConfigureAwait(false);
    Dictionary<string, object?> dict = new();
    while (reader.TokenType != JsonToken.EndObject)
    {
      switch (reader.TokenType)
      {
        case JsonToken.PropertyName:
          {
            var propName = reader.Value.NotNull().ToString().NotNull();
            await reader.ReadAsync(ct).ConfigureAwait(false); //goes prop value
            object? convertedValue = await ReadPropertyAsync(reader, ct).ConfigureAwait(false);
            dict[propName] = convertedValue;
            await reader.ReadAsync(ct).ConfigureAwait(false); //goes to next
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
      if (_closures.TryGetValue(objId, out Base? closure))
      {
        return closure;
      }

      throw new InvalidOperationException("missing reference");
    }

    return DictionaryConverter.Dict2Base(dict);
  }

  private async ValueTask<object?> ReadPropertyAsync(JsonReader reader, CancellationToken ct)
  {
    ct.ThrowIfCancellationRequested();
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
        return await ReadArrayAsync(reader, ct).ConfigureAwait(false);
      case JsonToken.StartObject:
        var dict = await ReadObjectAsync(reader, ct).ConfigureAwait(false);
        return dict;

      default:
        throw new ArgumentException("Json value not supported: " + reader.ValueType);
    }
  }
}
