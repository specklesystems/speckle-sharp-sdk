using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
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
  private volatile bool _isBusy;
  private readonly object _callbackLock = new();
  private readonly object?[] _invokeNull = [null];

  // id -> Base if already deserialized or id -> Task<object> if was handled by a bg thread
  private ConcurrentDictionary<string, object?>? _deserializedObjects;

  /// <summary>
  /// Property that describes the type of the object.
  /// </summary>
  private const string TYPE_DISCRIMINATOR = nameof(Base.speckle_type);

  public CancellationToken CancellationToken { get; set; }

  /// <summary>
  /// The sync transport. This transport will be used synchronously.
  /// </summary>
  public ITransport ReadTransport { get; set; }

  public Action<ProgressArgs>? OnProgressAction { get; set; }

  private long _currentCount;
  private readonly HashSet<string> _ids = new();
  private long _processedCount;

  public string? BlobStorageFolder { get; set; }

  /// <param name="rootObjectJson">The JSON string of the object to be deserialized <see cref="Base"/></param>
  /// <returns>A <see cref="Base"/> typed object deserialized from the <paramref name="rootObjectJson"/></returns>
  /// <exception cref="InvalidOperationException">Thrown when <see cref="_isBusy"/></exception>
  /// <exception cref="ArgumentNullException"><paramref name="rootObjectJson"/> was null</exception>
  /// <exception cref="SpeckleDeserializeException"><paramref name="rootObjectJson"/> cannot be deserialised to type <see cref="Base"/></exception>
  // /// <exception cref="TransportException"><see cref="ReadTransport"/> did not contain the required json objects (closures)</exception>
  public async Task<Base> DeserializeJsonAsync([NotNull] string? rootObjectJson)
  {
    var activity = SpeckleActivityFactory.Start();

    try
    {
      if (_isBusy)
      {
        throw new InvalidOperationException(
          "A deserializer instance can deserialize only 1 object at a time. Consider creating multiple deserializer instances"
        );
      }

      if (rootObjectJson is null)
      {
        throw new ArgumentNullException(
          nameof(rootObjectJson),
          $"Cannot deserialize {nameof(rootObjectJson)}, value was null"
        );
      }

      _isBusy = true;
      _deserializedObjects = new(StringComparer.Ordinal);
      _currentCount = 0;

      var result = (Base)await DeserializeJsonAsyncInternal(rootObjectJson).NotNull().ConfigureAwait(false);
      activity?.SetStatus(SpeckleActivityStatusCode.Ok);
      return result;
    }
    catch (Exception ex)
    {
      activity?.SetStatus(SpeckleActivityStatusCode.Error);
      activity?.RecordException(ex);
      throw;
    }
    finally
    {
      _deserializedObjects = null;
      _isBusy = false;
    }
  }

  private async Task<object?> DeserializeJsonAsyncInternal(string objectJson)
  {
    // Apparently this automatically parses DateTimes in strings if it matches the format:
    // JObject doc1 = JObject.Parse(objectJson);

    // This is equivalent code that doesn't parse datetimes:
    using JsonReader reader = new JsonTextReader(new StringReader(objectJson));

    reader.DateParseHandling = DateParseHandling.None;

    object? converted;
    try
    {
      await reader.ReadAsync(CancellationToken).ConfigureAwait(false);
      converted = await ReadObjectAsync(reader, CancellationToken).ConfigureAwait(false);
    }
    catch (Exception ex) when (!ex.IsFatal() && ex is not OperationCanceledException)
    {
      throw new SpeckleDeserializeException("Failed to deserialize", ex);
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

  private async Task<object?> ReadObjectAsync(JsonReader reader, CancellationToken ct)
  {
    await reader.ReadAsync(ct).ConfigureAwait(false);
    Dictionary<string, object?> dict = new();
    while (reader.TokenType != JsonToken.EndObject)
    {
      switch (reader.TokenType)
      {
        case JsonToken.PropertyName:
          {
            string propName = (reader.Value?.ToString()).NotNull();
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

    if (!dict.TryGetValue(TYPE_DISCRIMINATOR, out object? speckleType))
    {
      return dict;
    }

    if (speckleType as string == "reference" && dict.TryGetValue("referencedId", out object? referencedId))
    {
      var objId = (string)referencedId.NotNull();
      object? deserialized = await TryGetDeserializedAsync(objId).ConfigureAwait(false);
      return deserialized;
    }

    return Dict2Base(dict);
  }

  private async Task<object?> TryGetDeserializedAsync(string objId)
  {
    object? deserialized = null;
    _deserializedObjects.NotNull();
    if (_deserializedObjects.TryGetValue(objId, out object? o))
    {
      deserialized = o;
    }

    if (deserialized is Task<object> task)
    {
      try
      {
        deserialized = task.Result;
      }
      catch (AggregateException ex)
      {
        throw new SpeckleDeserializeException("Failed to deserialize reference object", ex);
      }

      if (_deserializedObjects.TryAdd(objId, deserialized))
      {
        _currentCount++;
      }
    }
    if (deserialized != null)
    {
      return deserialized;
    }

    // This reference was not already deserialized. Do it now in sync mode
    string? objectJson = await ReadTransport.GetObject(objId).ConfigureAwait(false);
    if (objectJson is null)
    {
      return null;
    }

    deserialized = await DeserializeJsonAsyncInternal(objectJson).ConfigureAwait(false);

    if (_deserializedObjects.NotNull().TryAdd(objId, deserialized))
    {
      _currentCount++;
    }

    return deserialized;
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
        if (reader.Value is long longValue)
        {
          return longValue;
        }
        if (reader.Value is BigInteger bitInt)
        {
          // This is behaviour carried over from v2 to facilitate large numbers from Python
          // This is quite hacky, as it's a bit questionable exactly what numbers are supported, and with what tolerance
          // For this reason, this can be considered undocumented behaviour, and is only for values within the range of a 64bit integer.
          return (double)bitInt;
        }

        throw new ArgumentException(
          $"Found an unsupported integer type {reader.Value?.GetType()} with value {reader.Value}"
        );
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

  private Base Dict2Base(Dictionary<string, object?> dictObj)
  {
    string typeName = (string)dictObj[TYPE_DISCRIMINATOR].NotNull();
    Type type = TypeLoader.GetType(typeName);
    Base baseObj = (Base)Activator.CreateInstance(type).NotNull();

    dictObj.Remove(TYPE_DISCRIMINATOR);
    dictObj.Remove("__closure");

    var staticProperties = TypeCache.GetTypeProperties(typeName);
    foreach (var entry in dictObj)
    {
      if (staticProperties.TryGetValue(entry.Key, out PropertyInfo? value) && value.CanWrite)
      {
        if (entry.Value == null)
        {
          // Check for JsonProperty(NullValueHandling = NullValueHandling.Ignore) attribute
          JsonPropertyAttribute? attr = TypeLoader.GetJsonPropertyAttribute(value);
          if (attr is { NullValueHandling: NullValueHandling.Ignore })
          {
            continue;
          }
        }

        Type targetValueType = value.PropertyType;
        bool conversionOk = ValueConverter.ConvertValue(targetValueType, entry.Value, out object? convertedValue);
        if (conversionOk)
        {
          value.SetValue(baseObj, convertedValue);
        }
        else
        {
          // Cannot convert the value in the json to the static property type
          throw new SpeckleDeserializeException(
            $"Cannot deserialize {entry.Value?.GetType().FullName} to {targetValueType.FullName}"
          );
        }
      }
      else
      {
        // No writable property with this name
        CallSiteCache.SetValue(entry.Key, baseObj, entry.Value);
      }
    }

    if (baseObj is Blob bb && BlobStorageFolder != null)
    {
      bb.filePath = bb.GetLocalDestinationPath(BlobStorageFolder);
    }

    var onDeserializedCallbacks = TypeCache.GetOnDeserializedCallbacks(typeName);
    foreach (MethodInfo onDeserialized in onDeserializedCallbacks)
    {
      onDeserialized.Invoke(baseObj, _invokeNull);
    }

    return baseObj;
  }
}
