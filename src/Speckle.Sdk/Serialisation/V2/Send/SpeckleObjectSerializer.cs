using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics.Contracts;
using System.Drawing;
using System.Globalization;
using Speckle.DoubleNumerics;
using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Common;
using Speckle.Sdk.Helpers;
using Speckle.Sdk.Models;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Serialisation.V2.Send;

public class SpeckleObjectSerializer2
{
  private List<Dictionary<string, int>> _parentClosures = new();
  private HashSet<object> _parentObjects = new();
  private readonly IProgress<ProgressArgs>? _onProgressAction;

  private readonly bool _trackDetachedChildren;
  private long _serializedCount;
  private readonly ISpeckleBasePropertyGatherer _propertyGatherer;
  private readonly ConcurrentDictionary<string, string> _idToJson;

  /// <summary>
  /// Keeps track of all detached children created during serialisation that have an applicationId (provided this serializer instance has been told to track detached children).
  /// This is currently used to cache previously converted objects and avoid their conversion if they haven't changed. See the DUI3 send bindings in rhino or another host app.
  /// </summary>
  public Dictionary<string, ObjectReference> ObjectReferences { get; } = new();

  public CancellationToken CancellationToken { get; set; }

  /// <summary>
  /// Creates a new Serializer instance.
  /// </summary>
  /// <param name="onProgressAction">Used to track progress.</param>
  /// <param name="trackDetachedChildren">Whether to store all detachable objects while serializing. They can be retrieved via <see cref="ObjectReferences"/> post serialization.</param>
  /// <param name="cancellationToken"></param>
  public SpeckleObjectSerializer2(ISpeckleBasePropertyGatherer propertyGatherer, 
    ConcurrentDictionary<string, string> idToJson, 
    IProgress<ProgressArgs>? onProgressAction = null,
    bool trackDetachedChildren = false,
    CancellationToken cancellationToken = default
  )
  {
    _propertyGatherer = propertyGatherer;
    _idToJson = idToJson;
    _onProgressAction = onProgressAction;
    CancellationToken = cancellationToken;
    _trackDetachedChildren = trackDetachedChildren;
  }

  /// <param name="baseObj">The object to serialize</param>
  /// <returns>The serialized JSON</returns>
  /// <exception cref="InvalidOperationException">The serializer is busy (already serializing an object)</exception>
  /// <exception cref="SpeckleSerializeException">Failed to extract (pre-serialize) properties from the <paramref name="baseObj"/></exception>
  public async ValueTask<string> SerializeAsync(Base baseObj)
  {
    try
    {
      try
      {
        var result = await SerializeBase(baseObj,  baseObj, true).NotNull().ConfigureAwait(false);
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
    }
  }

  // `Preserialize` means transforming all objects into the final form that will appear in json, with basic .net objects
  // (primitives, lists and dictionaries with string keys)
  private async ValueTask SerializeProperty(Base rootObj,
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
        Dictionary<string, object?> ret =
          new()
          {
            ["speckle_type"] = r.speckle_type,
            ["referencedId"] = r.referencedId,
            ["__closure"] = r.closure,
          };
        if (r.closure is not null)
        {
          foreach (var kvp in r.closure)
          {
            UpdateParentClosures(kvp.Key);
          }
        }
        UpdateParentClosures(r.referencedId);
        await SerializeProperty(rootObj, ret, writer).ConfigureAwait(false);
        break;
      case Base b:
        var result = await SerializeBase(rootObj, b, computeClosures, inheritedDetachInfo).ConfigureAwait(false);
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
            await SerializeProperty(rootObj, kvp.Value, writer, inheritedDetachInfo: inheritedDetachInfo).ConfigureAwait(false);
          }
          writer.WriteEndObject();
        }
        break;
      case ICollection e:
        {
          writer.WriteStartArray();
          foreach (object? element in e)
          {
            await SerializeProperty(rootObj, element, writer, inheritedDetachInfo: inheritedDetachInfo).ConfigureAwait(false);
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
      case System.Numerics.Matrix4x4:
        throw new ArgumentException("Please use Speckle.DoubleNumerics.Matrix4x4 instead", nameof(obj));
      default:
        throw new ArgumentException($"Unsupported value in serialization: {obj.GetType()}", nameof(obj));
    }
  }

  private async ValueTask<SerializationResult?> SerializeBase(Base rootObj,
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

    string? json;
    string id;
    if (rootObj == baseObj)
    {
      using var writer = new StringWriter();
      using var jsonWriter = SpeckleObjectSerializerPool.Instance.GetJsonTextWriter(writer);
     id = await SerializeBaseObject(rootObj, baseObj, jsonWriter, closure).ConfigureAwait(false);
       json = writer.ToString();
    }
    else if (!_idToJson.TryGetValue(baseObj.id, out  json))
    {
      throw new NotImplementedException("Serializing dictionaries that are not supported");
    }
    else
    {
       id = baseObj.id;
    }

    if (computeClosures || inheritedDetachInfo.IsDetachable || baseObj is Blob)
    {
      _parentClosures.RemoveAt(_parentClosures.Count - 1);
    }

    _parentObjects.Remove(baseObj);

    if (baseObj is Blob)
    {
      throw new NotSupportedException();
      /*StoreBlob(myBlob);
      UpdateParentClosures($"blob:{id}");
      return new(json, id);*/
    }

    if (inheritedDetachInfo.IsDetachable)
    {
      ObjectReference objRef = new() { referencedId = id };
      using var writer2 = new StringWriter();
      using var jsonWriter2 = SpeckleObjectSerializerPool.Instance.GetJsonTextWriter(writer2);
      await SerializeProperty(rootObj, objRef, jsonWriter2).ConfigureAwait(false);
      var json2 = writer2.ToString();
      UpdateParentClosures(id);

      _onProgressAction?.Report(new(ProgressEvent.SerializeObject, ++_serializedCount, null));

      // add to obj refs to return
      if (baseObj.applicationId != null && _trackDetachedChildren) // && baseObj is not DataChunk && baseObj is not Abstract) // not needed, as data chunks will never have application ids, and abstract objs are not really used.
      {
        ObjectReferences[baseObj.applicationId] = new ObjectReference()
        {
          referencedId = id,
          applicationId = baseObj.applicationId,
          closure = closure,
        };
      }
      return new(json2, null);
    }
    return new(json.NotNull(), id);
  }


  private async ValueTask<string> SerializeBaseObject(Base rootObj, 
    Base baseObj,
    JsonWriter writer,
    IReadOnlyDictionary<string, int> closure
  )
  {
    var allProperties = _propertyGatherer.ExtractAllProperties(baseObj);

    if (baseObj is not Blob)
    {
      writer = new SerializerIdWriter(writer);
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
      await SerializeProperty(rootObj, prop.Value.value, writer, prop.Value.info).ConfigureAwait(false);
    }

    string id;
    if (writer is SerializerIdWriter serializerIdWriter)
    {
      (var json, writer) = serializerIdWriter.FinishIdWriter();
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

  private async ValueTask SerializeProperty(Base rootObj, object? baseValue, JsonWriter jsonWriter, PropertyAttributeInfo detachInfo)
  {
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
      await SerializeProperty(rootObj, chunks, jsonWriter, inheritedDetachInfo: new PropertyAttributeInfo(true, false, 0, null))
        .ConfigureAwait(false);
      return;
    }

    await SerializeProperty(rootObj, baseValue, jsonWriter, inheritedDetachInfo: detachInfo).ConfigureAwait(false);
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
}