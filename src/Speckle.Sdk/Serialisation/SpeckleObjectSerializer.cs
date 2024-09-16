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
  public string Serialize(Base baseObj)
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
        var result = SerializeBase(baseObj, true).NotNull();
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
  private void SerializeProperty(
    object? obj,
    JsonWriter writer,
    bool computeClosures = false,
    PropertyAttributeInfo inheritedDetachInfo = default
  )
  {
    CancellationToken.ThrowIfCancellationRequested();

    if (obj == null)
    {
      writer.WriteNull();
      return;
    }

    if (obj.GetType().IsPrimitive || obj is string)
    {
      writer.WriteValue(obj);
      return;
    }

    switch (obj)
    {
      // Start with object references so they're not captured by the Base class case below
      // Note: this change was needed as we've made the ObjectReference type inherit from Base for
      // the purpose of the "do not convert unchanged previously converted objects" POC.
      case ObjectReference r:
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
        SerializeProperty(ret, writer);
        break;
      case Base b:
        var result = SerializeBase(b, computeClosures, inheritedDetachInfo);
        if (result is not null)
        {
          writer.WriteRawValue(result.Json);
        }
        else
        {
          writer.WriteNull();
        }
        break;
      case IDictionary d:
        {
          writer.WriteStartObject();

          foreach (DictionaryEntry kvp in d)
          {
            if (kvp.Key is not string key)
            {
              throw new ArgumentException(
                "Serializing dictionaries that are not string based keys is not supported",
                nameof(obj)
              );
            }

            writer.WritePropertyName(key);
            SerializeProperty(kvp.Value, writer, inheritedDetachInfo: inheritedDetachInfo);
          }
          writer.WriteEndObject();
        }
        break;
      case ICollection e:
        {
          writer.WriteStartArray();
          foreach (object? element in e)
          {
            SerializeProperty(element, writer, inheritedDetachInfo: inheritedDetachInfo);
          }
          writer.WriteEndArray();
        }
        break;
      case Enum:
        writer.WriteValue((int)obj);
        break;
      // Support for simple types
      case Guid g:
        writer.WriteValue(g.ToString());
        break;
      case Color c:
        writer.WriteValue(c.ToArgb());
        break;
      case DateTime t:
        writer.WriteValue(t.ToString("o", CultureInfo.InvariantCulture));
        break;
      case Matrix4x4 md:
        writer.WriteStartArray();

        writer.WriteValue(md.M11);
        writer.WriteValue(md.M12);
        writer.WriteValue(md.M13);
        writer.WriteValue(md.M14);
        writer.WriteValue(md.M21);
        writer.WriteValue(md.M22);
        writer.WriteValue(md.M23);
        writer.WriteValue(md.M24);
        writer.WriteValue(md.M31);
        writer.WriteValue(md.M32);
        writer.WriteValue(md.M33);
        writer.WriteValue(md.M34);
        writer.WriteValue(md.M41);
        writer.WriteValue(md.M42);
        writer.WriteValue(md.M43);
        writer.WriteValue(md.M44);
        writer.WriteEndArray();
        break;
      //BACKWARDS COMPATIBILITY: matrix4x4 changed from System.Numerics float to System.DoubleNumerics double in release 2.16
      case System.Numerics.Matrix4x4 ms:
        SpeckleLog.Logger.Warning(
          "This kept for backwards compatibility, no one should be using {this}",
          "BaseObjectSerializerV2 serialize System.Numerics.Matrix4x4"
        );
        writer.WriteStartArray();
        writer.WriteValue((double)ms.M11);
        writer.WriteValue((double)ms.M12);
        writer.WriteValue((double)ms.M13);
        writer.WriteValue((double)ms.M14);
        writer.WriteValue((double)ms.M21);
        writer.WriteValue((double)ms.M22);
        writer.WriteValue((double)ms.M23);
        writer.WriteValue((double)ms.M24);
        writer.WriteValue((double)ms.M31);
        writer.WriteValue((double)ms.M32);
        writer.WriteValue((double)ms.M33);
        writer.WriteValue((double)ms.M34);
        writer.WriteValue((double)ms.M41);
        writer.WriteValue((double)ms.M42);
        writer.WriteValue((double)ms.M43);
        writer.WriteValue((double)ms.M44);
        writer.WriteEndArray();
        break;
      default:
        throw new ArgumentException($"Unsupported value in serialization: {obj.GetType()}");
    }
  }

  internal SerializationResult? SerializeBase(
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
    string id = SerializeBaseObject(baseObj, jsonWriter, closure);
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
      return new(json, id);
    }

    if (inheritedDetachInfo.IsDetachable && WriteTransports.Count > 0)
    {
      StoreObject(id, json);

      ObjectReference objRef = new() { referencedId = id };
      using var writer2 = new StringWriter();
      using var jsonWriter2 = new JsonTextWriter(writer2);
      SerializeProperty(objRef, jsonWriter2);
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
      return new(json2, null);
    }
    return new(json, id);
  }

  private Dictionary<string, (object? value, PropertyAttributeInfo info)> ExtractAllProperties(Base baseObj)
  {
    IReadOnlyList<(PropertyInfo, PropertyAttributeInfo)> typedProperties = GetTypedPropertiesWithCache(baseObj);
    IReadOnlyCollection<string> dynamicProperties = baseObj.DynamicPropertyKeys;

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
#if NETSTANDARD2_0
      bool isDetachable = propName.StartsWith("@");
#else
      bool isDetachable = propName.StartsWith('@');
#endif
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

  private string SerializeBaseObject(Base baseObj, JsonWriter writer, IReadOnlyDictionary<string, int> closure)
  {
    var allProperties = ExtractAllProperties(baseObj);

    SerializerIdWriter? serializerIdWriter = null;
    if (baseObj is not Blob)
    {
      serializerIdWriter = new SerializerIdWriter(writer);
    }

    writer.WriteStartObject();
    // Convert all properties
    foreach (var prop in allProperties)
    {
      if (prop.Value.info.JsonPropertyInfo is { NullValueHandling: NullValueHandling.Ignore })
      {
        continue;
      }

      writer.WritePropertyName(prop.Key);
      SerializeProperty(prop.Value.value, writer, prop.Value.info);
    }

    string id;
    if (serializerIdWriter is not null)
    {
      var json = serializerIdWriter.FinishIdWriter();
      ((IDisposable)serializerIdWriter).Dispose();
      id = ComputeId(json);
    }
    else
    {
      id = ((Blob)baseObj).id;
    }
    writer.WritePropertyName("id");
    writer.WriteValue(id);
    baseObj.id = id;

    if (closure.Count > 0)
    {
      writer.WritePropertyName("__closure");
      writer.WriteStartObject();
      foreach (var c in closure)
      {
        writer.WritePropertyName(c.Key);
        writer.WriteValue(c.Value);
      }
      writer.WriteEndObject();
    }

    writer.WriteEndObject();
    return id;
  }

  private void SerializeProperty(object? baseValue, JsonWriter jsonWriter, PropertyAttributeInfo detachInfo)
  {
    // If there are no WriteTransports, keep everything attached.
    if (WriteTransports.Count == 0)
    {
      SerializeProperty(baseValue, jsonWriter, inheritedDetachInfo: detachInfo);
      return;
    }

    if (baseValue is IEnumerable chunkableCollection && detachInfo.IsChunkable)
    {
      List<DataChunk> chunks = new();
      DataChunk crtChunk = new() { data = new List<object?>(detachInfo.ChunkSize) };

      foreach (object element in chunkableCollection)
      {
        crtChunk.data.Add(element);
        if (crtChunk.data.Count >= detachInfo.ChunkSize)
        {
          chunks.Add(crtChunk);
          crtChunk = new DataChunk { data = new List<object?>(detachInfo.ChunkSize) };
        }
      }

      if (crtChunk.data.Count > 0)
      {
        chunks.Add(crtChunk);
      }
      SerializeProperty(chunks, jsonWriter, inheritedDetachInfo: new PropertyAttributeInfo(true, false, 0, null));
      return;
    }

    SerializeProperty(baseValue, jsonWriter, inheritedDetachInfo: detachInfo);
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
  private static string ComputeId(string serialized)
  {
#if NET6_0_OR_GREATER
    string hash = Crypt.Sha256(serialized.AsSpan(), length: HashUtility.HASH_LENGTH);
#else
    string hash = Crypt.Sha256(serialized, length: HashUtility.HASH_LENGTH);
#endif
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

    if (
      _typedPropertiesCache.TryGetValue(
        type.FullName.NotNull(),
        out List<(PropertyInfo, PropertyAttributeInfo)>? cached
      )
    )
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

      List<DetachPropertyAttribute> detachableAttributes = typedProperty
        .GetCustomAttributes<DetachPropertyAttribute>(true)
        .ToList();
      List<ChunkableAttribute> chunkableAttributes = typedProperty
        .GetCustomAttributes<ChunkableAttribute>(true)
        .ToList();
      bool isDetachable = detachableAttributes.Count > 0 && detachableAttributes[0].Detachable;
      bool isChunkable = chunkableAttributes.Count > 0;
      int chunkSize = isChunkable ? chunkableAttributes[0].MaxObjCountPerChunk : 1000;
      JsonPropertyAttribute? jsonPropertyAttribute = typedProperty.GetCustomAttribute<JsonPropertyAttribute>();
      ret.Add((typedProperty, new PropertyAttributeInfo(isDetachable, isChunkable, chunkSize, jsonPropertyAttribute)));
    }

    _typedPropertiesCache[type.FullName] = ret;
    return ret;
  }

  internal readonly struct PropertyAttributeInfo
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
