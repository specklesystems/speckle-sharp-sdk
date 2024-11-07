using System.Collections;
using System.Drawing;
using System.Globalization;
using Speckle.DoubleNumerics;
using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Common;
using Speckle.Sdk.Helpers;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation.Utilities;
using Speckle.Sdk.Transports;

// ReSharper disable MethodHasAsyncOverloadWithCancellation

namespace Speckle.Sdk.Serialisation.V2.Send;

public sealed class SpeckleObjectSerializer2
{
  private readonly HashSet<object> _parentObjects = new();
  private readonly IProgress<ProgressArgs>? _onProgressAction;
  private readonly IBasePropertyGatherer _basePropertyGatherer;

  private readonly bool _trackDetachedChildren;
  private int _serializedCount;

  /// <summary>
  /// Keeps track of all detached children created during serialisation that have an applicationId (provided this serializer instance has been told to track detached children).
  /// This is currently used to cache previously converted objects and avoid their conversion if they haven't changed. See the DUI3 send bindings in rhino or another host app.
  /// </summary>
  public Dictionary<string, ObjectReference> ObjectReferences { get; } = new();

  /// <summary>The sync transport. This transport will be used synchronously.</summary>
  public SerializeProcess SerializeProcess { get; }

  public CancellationToken CancellationToken { get; set; }

  public SpeckleObjectSerializer2(
    SerializeProcess serializeProcess,
    IBasePropertyGatherer basePropertyGatherer,
    IProgress<ProgressArgs>? onProgressAction = null,
    bool trackDetachedChildren = false,
    CancellationToken cancellationToken = default
  )
  {
    SerializeProcess = serializeProcess;
    _basePropertyGatherer = basePropertyGatherer;
    _onProgressAction = onProgressAction;
    CancellationToken = cancellationToken;
    _trackDetachedChildren = trackDetachedChildren;
  }

  public async ValueTask<string> Serialize(Base baseObj)
  {
    try
    {
      var result = await SerializeBase(baseObj, new(), true).NotNull().ConfigureAwait(false);
      await StoreObject(result.Id.NotNull(), result.Json).ConfigureAwait(false);
      return result.Json;
    }
    catch (Exception ex) when (!ex.IsFatal() && ex is not OperationCanceledException)
    {
      throw new SpeckleSerializeException($"Failed to extract (pre-serialize) properties from the {baseObj}", ex);
    }
  }

  // `Preserialize` means transforming all objects into the final form that will appear in json, with basic .net objects
  // (primitives, lists and dictionaries with string keys)
  private async ValueTask SerializeProperty(
    object? obj,
    JsonWriter writer,
    List<Dictionary<string, int>> closures,
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
            UpdateParentClosures(closures, kvp.Key);
          }
        }
        UpdateParentClosures(closures, r.referencedId);
        await SerializeProperty(ret, writer, closures).ConfigureAwait(false);
        break;
      case Base b:
        var result = await SerializeBase(b, closures, false, inheritedDetachInfo).ConfigureAwait(false);
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
            await SerializeProperty(kvp.Value, writer, closures, inheritedDetachInfo: inheritedDetachInfo)
              .ConfigureAwait(false);
          }
          writer.WriteEndObject();
        }
        break;
      case ICollection e:
        {
          writer.WriteStartArray();
          foreach (object? element in e)
          {
            await SerializeProperty(element, writer, closures, inheritedDetachInfo: inheritedDetachInfo)
              .ConfigureAwait(false);
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

  private async ValueTask<SerializationResult?> SerializeBase(
    Base baseObj,
    List<Dictionary<string, int>> closures,
    bool isRoot,
    PropertyAttributeInfo inheritedDetachInfo = default
  )
  {
    // handle circular references
    bool alreadySerialized = !_parentObjects.Add(baseObj);
    if (alreadySerialized)
    {
      return null;
    }

    string id;
    string json;
    Dictionary<string, int> closure = new();
    if (isRoot || inheritedDetachInfo.IsDetachable || baseObj is Blob)
    {
      closures.Add(closure);
    }

    using var writer = new StringWriter();
    using var jsonWriter = SpeckleObjectSerializerPool.Instance.GetJsonTextWriter(writer);
    id = await SerializeBaseObject(baseObj, jsonWriter, closure, closures).ConfigureAwait(false);
    json = writer.ToString();

    if (isRoot || inheritedDetachInfo.IsDetachable || baseObj is Blob)
    {
      closures.RemoveAt(closures.Count - 1);
    }

    _parentObjects.Remove(baseObj);

    if (baseObj is Blob)
    {
      /*StoreBlob(myBlob);
      UpdateParentClosures($"blob:{id}");
      return new(json, id);*/
      throw new NotSupportedException("Don't store blobs");
    }

    if (inheritedDetachInfo.IsDetachable)
    {
      await StoreObject(id, json).ConfigureAwait(false);

      var json2 = ReferenceGenerator.CreateReference(id);
      UpdateParentClosures(closures, id);

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
    return new(json, id);
  }

  private async ValueTask<string> SerializeBaseObject(
    Base baseObj,
    JsonWriter writer,
    IReadOnlyDictionary<string, int> closure,
    List<Dictionary<string, int>> closures
  )
  {
    if (baseObj is not Blob)
    {
      writer = new SerializerIdWriter(writer);
    }

    writer.WriteStartObject();
    // Convert all properties
    foreach (var prop in _basePropertyGatherer.ExtractAllProperties(baseObj))
    {
      if (prop.PropertyAttributeInfo.JsonPropertyInfo is { NullValueHandling: NullValueHandling.Ignore })
      {
        continue;
      }

      writer.WritePropertyName(prop.Name);
      await SerializeMaybeChunksProperty(prop.Value, writer, closures, prop.PropertyAttributeInfo)
        .ConfigureAwait(false);
    }

    string id;
    if (writer is SerializerIdWriter serializerIdWriter)
    {
      (var json, writer) = serializerIdWriter.FinishIdWriter();
      id = IdGenerator.ComputeId(json);
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

  private async ValueTask SerializeMaybeChunksProperty(
    object? baseValue,
    JsonWriter jsonWriter,
    List<Dictionary<string, int>> closures,
    PropertyAttributeInfo detachInfo
  )
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
      await SerializeProperty(
          chunks,
          jsonWriter,
          closures,
          inheritedDetachInfo: new PropertyAttributeInfo(true, false, 0, null)
        )
        .ConfigureAwait(false);
      return;
    }

    await SerializeProperty(baseValue, jsonWriter, closures, inheritedDetachInfo: detachInfo).ConfigureAwait(false);
  }

  private static void UpdateParentClosures(List<Dictionary<string, int>> allClosures, string objectId)
  {
    for (int parentLevel = 0; parentLevel < allClosures.Count; parentLevel++)
    {
      int childDepth = allClosures.Count - parentLevel;
      if (!allClosures[parentLevel].TryGetValue(objectId, out int currentValue))
      {
        currentValue = childDepth;
      }

      allClosures[parentLevel][objectId] = Math.Min(currentValue, childDepth);
    }
  }

  private async ValueTask StoreObject(string objectId, string objectJson)
  {
    _onProgressAction?.Report(new(ProgressEvent.SerializeObject, ++_serializedCount, null));
    await SerializeProcess.Save(new(objectId, objectJson, true), CancellationToken).ConfigureAwait(false);
  }
}
