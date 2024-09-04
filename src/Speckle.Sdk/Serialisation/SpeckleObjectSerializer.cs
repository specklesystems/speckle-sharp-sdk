using System.Collections;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using Speckle.DoubleNumerics;
using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Common;
using Speckle.Sdk.Helpers;
using Speckle.Sdk.Logging;
using Speckle.Sdk.Models;
using Speckle.Sdk.Transports;
using Constants = Speckle.Sdk.Helpers.Constants;

namespace Speckle.Sdk.Serialisation;

public record SerializationResult(string Json, string? Id, object? Value);

public class SpeckleObjectSerializer
{
  private readonly Stopwatch _stopwatch = new();
  private volatile bool _isBusy;
  private List<Dictionary<string, int>> _parentClosures = new();
  private HashSet<object> _parentObjects = new();
  private readonly Dictionary<string, List<(PropertyInfo, PropertyAttributeInfo)>> _typedPropertiesCache = new();
  private readonly Action<ProgressArgs>? _onProgressAction;

  private readonly bool _trackDetachedChildren;
  private int _serializedCount;

  /// <summary>
  /// Keeps track of all detached children created during serialisation that have an applicationId (provided this serializer instance has been told to track detached children).
  /// This is currently used to cache previously converted objects and avoid their conversion if they haven't changed. See the DUI3 send bindings in rhino or another host app.
  /// </summary>
  public Dictionary<string, ObjectReference> ObjectReferences { get; } = new();

  /// <summary>The sync transport. This transport will be used synchronously.</summary>
  public IReadOnlyCollection<ITransport> WriteTransports { get; }

  public CancellationToken CancellationToken { get; set; }

  /// <summary>The current total elapsed time spent serializing</summary>
  public TimeSpan Elapsed => _stopwatch.Elapsed;

  public SpeckleObjectSerializer()
    : this(Array.Empty<ITransport>()) { }

  /// <summary>
  /// Creates a new Serializer instance.
  /// </summary>
  /// <param name="writeTransports">The transports detached children should be persisted to.</param>
  /// <param name="onProgressAction">Used to track progress.</param>
  /// <param name="trackDetachedChildren">Whether to store all detachable objects while serializing. They can be retrieved via <see cref="ObjectReferences"/> post serialization.</param>
  /// <param name="cancellationToken"></param>
  public SpeckleObjectSerializer(
    IReadOnlyCollection<ITransport> writeTransports,
    Action<ProgressArgs>? onProgressAction = null,
    bool trackDetachedChildren = false,
    CancellationToken cancellationToken = default
  )
  {
    WriteTransports = writeTransports;
    _onProgressAction = onProgressAction;
    CancellationToken = cancellationToken;
    _trackDetachedChildren = trackDetachedChildren;
  }

  /// <param name="baseObj">The object to serialize</param>
  /// <returns>The serialized JSON</returns>
  /// <exception cref="InvalidOperationException">The serializer is busy (already serializing an object)</exception>
  /// <exception cref="TransportException">Failed to save object in one or more <see cref="WriteTransports"/></exception>
  /// <exception cref="SpeckleSerializeException">Failed to extract (pre-serialize) properties from the <paramref name="baseObj"/></exception>
  /// <exception cref="OperationCanceledException">One or more <see cref="WriteTransports"/>'s cancellation token requested cancel</exception>
  public async Task<string> SerializeAsync(Base baseObj)
  {
    if (_isBusy)
    {
      throw new InvalidOperationException(
        "A serializer instance can serialize only 1 object at a time. Consider creating multiple serializer instances"
      );
    }

    try
    {
      _stopwatch.Start();
      _isBusy = true;
      try
      {
        var result = await SerializeBaseAsync(baseObj, true).NotNull();
        StoreObject(result.Id.NotNull(), result.Json);
        return result.Json;
      }
      catch (Exception ex) when (!ex.IsFatal() && ex is not OperationCanceledException)
      {
        throw new SpeckleSerializeException($"Failed to extract (pre-serialize) properties from the {baseObj}", ex);
      }
    }
    finally
    {
      _parentClosures = new List<Dictionary<string, int>>(); // cleanup in case of exceptions
      _parentObjects = new HashSet<object>();
      _isBusy = false;
      _stopwatch.Stop();
    }
  }

  // `Preserialize` means transforming all objects into the final form that will appear in json, with basic .net objects
  // (primitives, lists and dictionaries with string keys)
  public async Task<object?> SerializePropertyAsync(
    object? obj,
    JsonWriter writer,
    bool computeClosures = false,
    PropertyAttributeInfo inheritedDetachInfo = default
  )
  {
    CancellationToken.ThrowIfCancellationRequested();

    if (obj == null)
    {
      await writer.WriteNullAsync(CancellationToken);
      return null;
    }

    if (obj.GetType().IsPrimitive || obj is string)
    {
      await writer.WriteValueAsync(obj, CancellationToken);
      return obj;
    }

    switch (obj)
    {
      // Start with object references so they're not captured by the Base class case below
      // Note: this change was needed as we've made the ObjectReference type inherit from Base for
      // the purpose of the "do not convert unchanged previously converted objects" POC.
      case ObjectReference r:
      {
        Dictionary<string, object> ret =
          new()
          {
            ["speckle_type"] = r.speckle_type,
            ["referencedId"] = r.referencedId,
            ["__closure"] = r.closure
          };
        if (r.closure is not null)
        {
          foreach (var kvp in r.closure)
          {
            UpdateParentClosures(kvp.Key);
          }
        }
        UpdateParentClosures(r.referencedId);
        return await SerializePropertyAsync(ret, writer);
      }
      case Base b:
        var result = await SerializeBaseAsync(b, computeClosures, inheritedDetachInfo);
        if (result is not null)
        {
          await writer.WriteRawValueAsync(result.Json, CancellationToken);
          return result.Value;
        }
        await writer.WriteNullAsync(CancellationToken);
        return null;
      case IDictionary d:
      {
        await writer.WriteStartObjectAsync(CancellationToken);
        foreach (DictionaryEntry kvp in d)
        {
          await writer.WritePropertyNameAsync(kvp.Key.ToString(), CancellationToken);
          await SerializePropertyAsync(kvp.Value, writer, inheritedDetachInfo: inheritedDetachInfo);
        }
        await writer.WriteEndObjectAsync(CancellationToken);
        break;
      }
      case IEnumerable e:
      {
        //TODO: handle IReadonlyDictionary
        await writer.WriteStartArrayAsync(CancellationToken);
        foreach (object? element in e)
        {
          await SerializePropertyAsync(element, writer, inheritedDetachInfo: inheritedDetachInfo);
        }
        await writer.WriteEndArrayAsync(CancellationToken);
        break;
      }
      case Enum:
        await writer.WriteValueAsync((int)obj, CancellationToken);
        break;
      // Support for simple types
      case Guid g:
        await writer.WriteValueAsync(g.ToString(), CancellationToken);
        break;
      case Color c:
        await writer.WriteValueAsync(c.ToArgb(), CancellationToken);
        break;
      case DateTime t:
        await writer.WriteValueAsync(t.ToString("o", CultureInfo.InvariantCulture), CancellationToken);
        break;
      case Matrix4x4 md:
        await writer.WriteStartArrayAsync(CancellationToken);

        await writer.WriteValueAsync(md.M11, CancellationToken);
        await writer.WriteValueAsync(md.M12, CancellationToken);
        await writer.WriteValueAsync(md.M13, CancellationToken);
        await writer.WriteValueAsync(md.M14, CancellationToken);
        await writer.WriteValueAsync(md.M21, CancellationToken);
        await writer.WriteValueAsync(md.M22, CancellationToken);
        await writer.WriteValueAsync(md.M23, CancellationToken);
        await writer.WriteValueAsync(md.M24, CancellationToken);
        await writer.WriteValueAsync(md.M31, CancellationToken);
        await writer.WriteValueAsync(md.M32, CancellationToken);
        await writer.WriteValueAsync(md.M33, CancellationToken);
        await writer.WriteValueAsync(md.M34, CancellationToken);
        await writer.WriteValueAsync(md.M41, CancellationToken);
        await writer.WriteValueAsync(md.M42, CancellationToken);
        await writer.WriteValueAsync(md.M43, CancellationToken);
        await writer.WriteValueAsync(md.M44, CancellationToken);
        await writer.WriteEndArrayAsync(CancellationToken);
        break;
      //BACKWARDS COMPATIBILITY: matrix4x4 changed from System.Numerics float to System.DoubleNumerics double in release 2.16
      case System.Numerics.Matrix4x4 ms:
        SpeckleLog.Logger.Warning(
          "This kept for backwards compatibility, no one should be using {this}",
          "BaseObjectSerializerV2 serialize System.Numerics.Matrix4x4"
        );
        await writer.WriteStartArrayAsync(CancellationToken);
        await writer.WriteValueAsync((double)ms.M11, CancellationToken);
        await writer.WriteValueAsync((double)ms.M12, CancellationToken);
        await writer.WriteValueAsync((double)ms.M13, CancellationToken);
        await writer.WriteValueAsync((double)ms.M14, CancellationToken);
        await writer.WriteValueAsync((double)ms.M21, CancellationToken);
        await writer.WriteValueAsync((double)ms.M22, CancellationToken);
        await writer.WriteValueAsync((double)ms.M23, CancellationToken);
        await writer.WriteValueAsync((double)ms.M24, CancellationToken);
        await writer.WriteValueAsync((double)ms.M31, CancellationToken);
        await writer.WriteValueAsync((double)ms.M32, CancellationToken);
        await writer.WriteValueAsync((double)ms.M33, CancellationToken);
        await writer.WriteValueAsync((double)ms.M34, CancellationToken);
        await writer.WriteValueAsync((double)ms.M41, CancellationToken);
        await writer.WriteValueAsync((double)ms.M42, CancellationToken);
        await writer.WriteValueAsync((double)ms.M43, CancellationToken);
        await writer.WriteValueAsync((double)ms.M44, CancellationToken);
        await writer.WriteEndArrayAsync(CancellationToken);
        break;
      default:
        throw new ArgumentException($"Unsupported value in serialization: {obj.GetType()}");
    }

    return obj;
  }

  private async Task<SerializationResult?> SerializeBaseAsync(
    Base baseObj,
    bool computeClosures = false,
    PropertyAttributeInfo inheritedDetachInfo = default
  )
  {
    // handle circular references
    bool alreadySerialized = !_parentObjects.Add(baseObj);
    if (alreadySerialized)
    {
      return null;
    }

    Dictionary<string, int> closure = new();
    if (computeClosures || inheritedDetachInfo.IsDetachable || baseObj is Blob)
    {
      _parentClosures.Add(closure);
    }

    using var writer = new StringWriter();
    using var jsonWriter = new JsonTextWriter(writer);
    string id = await SerializeBaseObjectAsync(baseObj, jsonWriter, closure);
    var json = writer.ToString();

    if (computeClosures || inheritedDetachInfo.IsDetachable || baseObj is Blob)
    {
      _parentClosures.RemoveAt(_parentClosures.Count - 1);
    }

    _parentObjects.Remove(baseObj);

    if (baseObj is Blob myBlob)
    {
      StoreBlob(myBlob);
      UpdateParentClosures($"blob:{id}");
      return new(json, id, baseObj);
    }

    if (inheritedDetachInfo.IsDetachable && WriteTransports.Count > 0)
    {
      StoreObject(id, json);

      ObjectReference objRef = new() { referencedId = id };
      using var writer2 = new StringWriter();
      using var jsonWriter2 = new JsonTextWriter(writer2);
      var newObj = await SerializePropertyAsync(objRef, jsonWriter2);
      var json2 = writer2.ToString();
      UpdateParentClosures(id);

      _onProgressAction?.Invoke(new(ProgressEvent.SerializeObject, ++_serializedCount, null));

      // add to obj refs to return
      if (baseObj.applicationId != null && _trackDetachedChildren) // && baseObj is not DataChunk && baseObj is not Abstract) // not needed, as data chunks will never have application ids, and abstract objs are not really used.
      {
        ObjectReferences[baseObj.applicationId] = new ObjectReference()
        {
          referencedId = id,
          applicationId = baseObj.applicationId,
          closure = closure
        };
      }
      return new(json2, null, newObj);
    }
    return new(json, id, baseObj);
  }

  private Dictionary<string, (object?, PropertyAttributeInfo)> ExtractAllProperties(Base baseObj)
  {
    IReadOnlyList<(PropertyInfo, PropertyAttributeInfo)> typedProperties = GetTypedPropertiesWithCache(baseObj);
    IReadOnlyCollection<string> dynamicProperties = baseObj.GetDynamicPropertyKeys();

    // propertyName -> (originalValue, isDetachable, isChunkable, chunkSize)
    Dictionary<string, (object?, PropertyAttributeInfo)> allProperties =
      new(typedProperties.Count + dynamicProperties.Count);

    // Construct `allProperties`: Add typed properties
    foreach ((PropertyInfo propertyInfo, PropertyAttributeInfo detachInfo) in typedProperties)
    {
      object? baseValue = propertyInfo.GetValue(baseObj);
      allProperties[propertyInfo.Name] = (baseValue, detachInfo);
    }

    // Construct `allProperties`: Add dynamic properties
    foreach (string propName in dynamicProperties)
    {
      if (propName.StartsWith("__"))
      {
        continue;
      }

      object? baseValue = baseObj[propName];
      bool isDetachable = propName.StartsWith("@");
      bool isChunkable = false;
      int chunkSize = 1000;

      if (Constants.ChunkPropertyNameRegex.IsMatch(propName))
      {
        var match = Constants.ChunkPropertyNameRegex.Match(propName);
        isChunkable = int.TryParse(match.Groups[^1].Value, out chunkSize);
      }
      allProperties[propName] = (baseValue, new PropertyAttributeInfo(isDetachable, isChunkable, chunkSize, null));
    }

    return allProperties;
  }

  private async Task<string> SerializeBaseObjectAsync(
    Base baseObj,
    JsonWriter writer,
    IReadOnlyDictionary<string, int> closure
  )
  {
    var allProperties = ExtractAllProperties(baseObj);
    var computeIdProperties = new Dictionary<string, object?>(allProperties.Count);

    await writer.WriteStartObjectAsync(CancellationToken);
    // Convert all properties
    foreach (var prop in allProperties)
    {
      if (prop.Value.Item2.JsonPropertyInfo is { NullValueHandling: NullValueHandling.Ignore })
      {
        continue;
      }

      await writer.WritePropertyNameAsync(prop.Key, CancellationToken);
      var valueToComputeIdFor = await SerializePropertyAsync(prop.Value.Item1, writer, prop.Value.Item2);
      computeIdProperties[prop.Key] = valueToComputeIdFor;
    }

    var id = baseObj is Blob blob ? blob.id : ComputeId(computeIdProperties);
    await writer.WritePropertyNameAsync("id", CancellationToken);
    await writer.WriteValueAsync(id, CancellationToken);
    baseObj.id = id;

    if (closure.Count > 0)
    {
      await writer.WritePropertyNameAsync("__closure", CancellationToken);
      await writer.WriteStartObjectAsync(CancellationToken);
      foreach (var c in closure)
      {
        await writer.WritePropertyNameAsync(c.Key, CancellationToken);
        await writer.WriteValueAsync(c.Value, CancellationToken);
      }
      await writer.WriteEndObjectAsync(CancellationToken);
    }

    await writer.WriteEndObjectAsync(CancellationToken);
    return id;
  }

  private Task<object?> SerializePropertyAsync(
    object? baseValue,
    JsonWriter jsonWriter,
    PropertyAttributeInfo detachInfo
  )
  {
    // If there are no WriteTransports, keep everything attached.
    if (WriteTransports.Count == 0)
    {
      return SerializePropertyAsync(baseValue, jsonWriter, inheritedDetachInfo: detachInfo);
    }

    if (baseValue is IEnumerable chunkableCollection && detachInfo.IsChunkable)
    {
      List<object> chunks = new();
      DataChunk crtChunk = new() { data = new List<object>(detachInfo.ChunkSize) };

      foreach (object element in chunkableCollection)
      {
        crtChunk.data.Add(element);
        if (crtChunk.data.Count >= detachInfo.ChunkSize)
        {
          chunks.Add(crtChunk);
          crtChunk = new DataChunk { data = new List<object>(detachInfo.ChunkSize) };
        }
      }

      if (crtChunk.data.Count > 0)
      {
        chunks.Add(crtChunk);
      }

      return SerializePropertyAsync(
        chunks,
        jsonWriter,
        inheritedDetachInfo: new PropertyAttributeInfo(true, false, 0, null)
      );
    }

    return SerializePropertyAsync(baseValue, jsonWriter, inheritedDetachInfo: detachInfo);
  }

  private void UpdateParentClosures(string objectId)
  {
    for (int parentLevel = 0; parentLevel < _parentClosures.Count; parentLevel++)
    {
      int childDepth = _parentClosures.Count - parentLevel;
      if (!_parentClosures[parentLevel].TryGetValue(objectId, out int currentValue))
      {
        currentValue = childDepth;
      }

      _parentClosures[parentLevel][objectId] = Math.Min(currentValue, childDepth);
    }
  }

  [Pure]
  private static string ComputeId(IReadOnlyDictionary<string, object?> obj)
  {
    string serialized = JsonConvert.SerializeObject(obj);
    string hash = Crypt.Sha256(serialized, length: HashUtility.HASH_LENGTH);
    return hash;
  }

  private void StoreObject(string objectId, string objectJson)
  {
    _stopwatch.Stop();
    foreach (var transport in WriteTransports)
    {
      transport.SaveObject(objectId, objectJson);
    }

    _stopwatch.Start();
  }

  private void StoreBlob(Blob obj)
  {
    bool hasBlobTransport = false;

    _stopwatch.Stop();

    foreach (var transport in WriteTransports)
    {
      if (transport is IBlobCapableTransport blobTransport)
      {
        hasBlobTransport = true;
        blobTransport.SaveBlob(obj);
      }
    }

    _stopwatch.Start();
    if (!hasBlobTransport)
    {
      throw new InvalidOperationException(
        "Object tree contains a Blob (file), but the serializer has no blob saving capable transports."
      );
    }
  }

  // (propertyInfo, isDetachable, isChunkable, chunkSize, JsonPropertyAttribute)
  private IReadOnlyList<(PropertyInfo, PropertyAttributeInfo)> GetTypedPropertiesWithCache(Base baseObj)
  {
    Type type = baseObj.GetType();

    if (_typedPropertiesCache.TryGetValue(type.FullName, out List<(PropertyInfo, PropertyAttributeInfo)>? cached))
    {
      return cached;
    }

    var typedProperties = baseObj.GetInstanceMembers().ToList();
    List<(PropertyInfo, PropertyAttributeInfo)> ret = new(typedProperties.Count);

    foreach (PropertyInfo typedProperty in typedProperties)
    {
      if (typedProperty.Name.StartsWith("__") || typedProperty.Name == "id")
      {
        continue;
      }

      bool jsonIgnore = typedProperty.IsDefined(typeof(JsonIgnoreAttribute), false);
      if (jsonIgnore)
      {
        continue;
      }

      _ = typedProperty.GetValue(baseObj);

      List<DetachProperty> detachableAttributes = typedProperty.GetCustomAttributes<DetachProperty>(true).ToList();
      List<Chunkable> chunkableAttributes = typedProperty.GetCustomAttributes<Chunkable>(true).ToList();
      bool isDetachable = detachableAttributes.Count > 0 && detachableAttributes[0].Detachable;
      bool isChunkable = chunkableAttributes.Count > 0;
      int chunkSize = isChunkable ? chunkableAttributes[0].MaxObjCountPerChunk : 1000;
      JsonPropertyAttribute? jsonPropertyAttribute = typedProperty.GetCustomAttribute<JsonPropertyAttribute>();
      ret.Add((typedProperty, new PropertyAttributeInfo(isDetachable, isChunkable, chunkSize, jsonPropertyAttribute)));
    }

    _typedPropertiesCache[type.FullName] = ret;
    return ret;
  }

  public readonly struct PropertyAttributeInfo
  {
    public PropertyAttributeInfo(
      bool isDetachable,
      bool isChunkable,
      int chunkSize,
      JsonPropertyAttribute? jsonPropertyAttribute
    )
    {
      IsDetachable = isDetachable || isChunkable;
      IsChunkable = isChunkable;
      ChunkSize = chunkSize;
      JsonPropertyInfo = jsonPropertyAttribute;
    }

    public readonly bool IsDetachable;
    public readonly bool IsChunkable;
    public readonly int ChunkSize;
    public readonly JsonPropertyAttribute? JsonPropertyInfo;
  }
}