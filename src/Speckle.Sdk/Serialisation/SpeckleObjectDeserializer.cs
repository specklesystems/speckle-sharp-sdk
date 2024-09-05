using System.Collections.Concurrent;
using System.Reflection;
using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Common;
using Speckle.Sdk.Host;
using Speckle.Sdk.Logging;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation.Utilities;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Serialisation;

public sealed class SpeckleObjectDeserializer
{
  private bool _isBusy;
  private readonly object _callbackLock = new();

  public CancellationToken CancellationToken { get; set; }

  public Action<ProgressArgs>? OnProgressAction { get; set; }

  private long _currentCount;
  private readonly HashSet<string> _ids = new();
  private long _processedCount;

  /// <param name="rootObjectJson">The JSON string of the object to be deserialized <see cref="Base"/></param>
  /// <returns>A <see cref="Base"/> typed object deserialized from the <paramref name="rootObjectJson"/></returns>
  /// <exception cref="InvalidOperationException">Thrown when <see cref="_isBusy"/></exception>
  /// <exception cref="ArgumentNullException"><paramref name="rootObjectJson"/> was null</exception>
  /// <exception cref="SpeckleDeserializeException"><paramref name="rootObjectJson"/> cannot be deserialised to type <see cref="Base"/></exception>
  // /// <exception cref="TransportException"><see cref="ReadTransport"/> did not contain the required json objects (closures)</exception>
  public async Task<Dictionary<string, object?>> DeserializeJsonAsync(string rootObjectJson)
  {
    if (_isBusy)
    {
      throw new InvalidOperationException(
        "A deserializer instance can deserialize only 1 object at a time. Consider creating multiple deserializer instances"
      );
    }

    try
    {
      _isBusy = true;
      _currentCount = 0;
      return await DeserializeJsonAsyncInternal(rootObjectJson).ConfigureAwait(false);
    }
    finally
    {
      _isBusy = false;
    }
  }

  private async Task<Dictionary<string, object?>> DeserializeJsonAsyncInternal(string objectJson)
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

    Dictionary<string, object?> converted;
    try
    {
      await reader.ReadAsync(CancellationToken).ConfigureAwait(false);
      converted = await ReadObjectAsync(reader, CancellationToken).ConfigureAwait(false);
    }
    catch (Exception ex) when (!ex.IsFatal() && ex is not OperationCanceledException)
    {
      throw new SpeckleDeserializeException($"Failed to deserialize", ex);
    }

    lock (_callbackLock)
    {
      _processedCount++;
      OnProgressAction?.Invoke(
        new ProgressArgs(ProgressEvent.DeserializeObject, _currentCount, _ids.Count, _processedCount)
      );
    }

    return converted;
  }

  //this should be buffered
  private async Task<List<object?>> ReadArrayAsync(JsonReader reader, CancellationToken ct)
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

  private async Task<Dictionary<string, object?>> ReadObjectAsync(JsonReader reader, CancellationToken ct)
  {
    await reader.ReadAsync(ct).ConfigureAwait(false);
    Dictionary<string, object?> dict = new();
    while (reader.TokenType != JsonToken.EndObject)
    {
      switch (reader.TokenType)
      {
        case JsonToken.PropertyName:
          {
            var propName = reader.Value.NotNull().ToString();
            if (propName == "__closure")
            {
              await reader.ReadAsync(ct).ConfigureAwait(false); //goes to prop value
              var closures = await ClosureParser.GetClosuresAsync(reader).ConfigureAwait(false);
              foreach (var closure in closures)
              {
                _ids.Add(closure.Item1);
              }

              foreach (var closure in closures)
              {
                string objId = closure.Item1;
                //don't do anything with return value but later check if null
                // https://linear.app/speckle/issue/CXPLA-54/when-deserializing-dont-allow-closures-that-arent-downloadable
                await TryGetDeserializedAsync(objId).ConfigureAwait(false);
              }
              await reader.ReadAsync(ct).ConfigureAwait(false); //goes to next
              continue;
            }
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



   

    return dict;
  }
  
  private async Task<object?> ReadPropertyAsync(JsonReader reader, CancellationToken ct)
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
