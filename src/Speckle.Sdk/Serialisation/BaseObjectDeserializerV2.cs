using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Common;
using Speckle.Sdk.Host;
using Speckle.Sdk.Logging;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation.SerializationUtilities;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Serialisation;

public sealed class BaseObjectDeserializerV2
{
  private bool _isBusy;
  private readonly object _callbackLock = new();

  // id -> Base if already deserialized or id -> Task<object> if was handled by a bg thread
  private ConcurrentDictionary<string, object?>? _deserializedObjects;

  /// <summary>
  /// Property that describes the type of the object.
  /// </summary>
  private const string TYPE_DISCRIMINATOR = nameof(Base.speckle_type);

  private DeserializationWorkerThreads? _workerThreads;

  public CancellationToken CancellationToken { get; set; }

  /// <summary>
  /// The sync transport. This transport will be used synchronously.
  /// </summary>
  public ITransport ReadTransport { get; set; }

  public Action<ProgressArgs>? OnProgressAction { get; set; }

  public string? BlobStorageFolder { get; set; }
  public TimeSpan Elapsed { get; private set; }

  public static int DefaultNumberThreads => Math.Min(Environment.ProcessorCount, 6); //6 threads seems the sweet spot, see performance test project
  public int WorkerThreadCount { get; set; } = DefaultNumberThreads;

  /// <param name="rootObjectJson">The JSON string of the object to be deserialized <see cref="Base"/></param>
  /// <returns>A <see cref="Base"/> typed object deserialized from the <paramref name="rootObjectJson"/></returns>
  /// <exception cref="InvalidOperationException">Thrown when <see cref="_isBusy"/></exception>
  /// <exception cref="ArgumentNullException"><paramref name="rootObjectJson"/> was null</exception>
  /// <exception cref="SpeckleDeserializeException"><paramref name="rootObjectJson"/> cannot be deserialised to type <see cref="Base"/></exception>
  // /// <exception cref="TransportException"><see cref="ReadTransport"/> did not contain the required json objects (closures)</exception>
  public Base Deserialize(string rootObjectJson)
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
      var stopwatch = Stopwatch.StartNew();
      _deserializedObjects = new();
      _workerThreads = new DeserializationWorkerThreads(this, WorkerThreadCount);
      _workerThreads.Start();

      var closures = ClosureParser.GetClosures(rootObjectJson);
      int i = 0;
      foreach (var closure in closures)
      {
        string objId = closure.Item1;
        // pausing for getting object from the transport
        stopwatch.Stop();
        string? objJson = ReadTransport.GetObject(objId);

        //TODO: We should fail loudly when a closure can't be found (objJson is null)
        //but adding throw here breaks blobs tests, see CNX-8541

        stopwatch.Start();
        object? deserializedOrPromise = DeserializeTransportObjectProxy(objJson, i++, closures.Count);
        _deserializedObjects.TryAdd(objId, deserializedOrPromise);
      }

      object? ret;
      try
      {
        ret = DeserializeTransportObject(rootObjectJson, null, null);
      }
      catch (JsonReaderException ex)
      {
        throw new SpeckleDeserializeException("Failed to deserialize json", ex);
      }

      stopwatch.Stop();
      Elapsed += stopwatch.Elapsed;
      if (ret is not Base b)
      {
        throw new SpeckleDeserializeException(
          $"Expected {nameof(rootObjectJson)} to be deserialized to type {nameof(Base)} but was {ret}"
        );
      }

      return b;
    }
    finally
    {
      _deserializedObjects = null;
      _workerThreads?.Dispose();
      _workerThreads = null;
      _isBusy = false;
    }
  }

  private object? DeserializeTransportObjectProxy(string? objectJson, long? current, long? total)
  {
    if (objectJson is null)
    {
      return null;
    }
    // Try background work
    Task<object?>? bgResult = _workerThreads?.TryStartTask(
      WorkerThreadTaskType.Deserialize,
      objectJson,
      current,
      total
    ); //BUG: Because we don't guarantee this task will ever be awaited, this may lead to unobserved exceptions!
    if (bgResult != null)
    {
      return bgResult;
    }

    // SyncS
    return DeserializeTransportObject(objectJson, current, total);
  }

  internal object? DeserializeTransportObject(string objectJson, long? currentObjectCount, long? totalObjectCount)
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

    object? converted;
    try
    {
      converted = ConvertJsonElement(reader, currentObjectCount, totalObjectCount, CancellationToken);
    }
    catch (Exception ex) when (!ex.IsFatal() && ex is not OperationCanceledException)
    {
      throw new SpeckleDeserializeException($"Failed to deserialize", ex);
    }

    lock (_callbackLock)
    {
      OnProgressAction?.Invoke(new ProgressArgs(ProgressEvent.DeserializeObject, currentObjectCount, totalObjectCount));
    }

    return converted;
  }

  private List<object?> ReadArray(
    JsonReader reader,
    long? currentObjectCount,
    long? totalObjectCount,
    CancellationToken ct
  )
  {
    reader.Read();
    List<object?> retList = new();
    while (reader.TokenType != JsonToken.EndArray)
    {
      object? convertedValue = ConvertJsonElement(reader, currentObjectCount, totalObjectCount, ct);
      if (convertedValue is DataChunk chunk)
      {
        retList.AddRange(chunk.data);
      }
      else
      {
        retList.Add(convertedValue);
      }
    }
    return retList;
  }

  private Dictionary<string, object?> ReadObject(
    JsonReader reader,
    long? currentObjectCount,
    long? totalObjectCount,
    CancellationToken ct
  )
  {
    reader.Read();
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
              reader.Read(); //goes to prop vale
              reader.Skip();
              reader.Read(); //goes to next
              continue;
            }
            reader.Read(); //goes to prop vale
            object? convertedValue = ConvertJsonElement(reader, currentObjectCount, totalObjectCount, ct);
            dict[propName] = convertedValue;
          }
          break;
      }
    }
    return dict;
  }

  private object? ConvertJsonElement(
    JsonReader reader,
    long? currentObjectCount,
    long? totalObjectCount,
    CancellationToken ct
  )
  {
    reader.Read();
    while (reader.TokenType != JsonToken.EndObject)
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
          return ReadArray(reader, currentObjectCount, totalObjectCount, ct);
        case JsonToken.StartObject:
          var dict = ReadObject(reader, currentObjectCount, totalObjectCount, ct);

          if (!dict.TryGetValue(TYPE_DISCRIMINATOR, out object? speckleType))
          {
            return dict;
          }

          if (speckleType as string == "reference" && dict.TryGetValue("referencedId", out object? referencedId))
          {
            var objId = (string)referencedId.NotNull();
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

              _deserializedObjects.TryAdd(objId, deserialized);
            }

            if (deserialized != null)
            {
              return deserialized;
            }

            // This reference was not already deserialized. Do it now in sync mode
            string? objectJson = ReadTransport.GetObject(objId);
            if (objectJson is null)
            {
              throw new TransportException($"Failed to fetch object id {objId} from {ReadTransport} ");
            }

            deserialized = DeserializeTransportObject(objectJson, currentObjectCount, totalObjectCount);

            _deserializedObjects.TryAdd(objId, deserialized);

            return deserialized;
          }

          return Dict2Base(dict);
        default:
          throw new ArgumentException("Json value not supported: " + reader.ValueType);
      }
    }

    return null;
  }

  private Base Dict2Base(Dictionary<string, object?> dictObj)
  {
    string typeName = (string)dictObj[TYPE_DISCRIMINATOR].NotNull();
    Type type = TypeLoader.GetType(typeName);
    Base baseObj = (Base)Activator.CreateInstance(type);

    dictObj.Remove(TYPE_DISCRIMINATOR);
    dictObj.Remove("__closure");

    var staticProperties = BaseObjectSerializationUtilities.GetTypeProperties(typeName);
    foreach (var entry in dictObj)
    {
      if (staticProperties.TryGetValue(entry.Key, out PropertyInfo? value) && value.CanWrite)
      {
        if (entry.Value == null)
        {
          // Check for JsonProperty(NullValueHandling = NullValueHandling.Ignore) attribute
          JsonPropertyAttribute attr = value.GetCustomAttribute<JsonPropertyAttribute>(true);
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

    var onDeserializedCallbacks = BaseObjectSerializationUtilities.GetOnDeserializedCallbacks(typeName);
    foreach (MethodInfo onDeserialized in onDeserializedCallbacks)
    {
      onDeserialized.Invoke(baseObj, new object?[] { null });
    }

    return baseObj;
  }
}
