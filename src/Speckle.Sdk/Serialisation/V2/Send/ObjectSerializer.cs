using System.Collections;
using System.Drawing;
using System.Globalization;
using Speckle.DoubleNumerics;
using Speckle.InterfaceGenerator;
using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Common;
using Speckle.Sdk.Dependencies;
using Speckle.Sdk.Helpers;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation.Utilities;
using Closures = System.Collections.Generic.Dictionary<Speckle.Sdk.Serialisation.Id, int>;

namespace Speckle.Sdk.Serialisation.V2.Send;

public readonly record struct NodeInfo(Json Json, Closures? C)
{
  public Closures GetClosures(CancellationToken cancellationToken) =>
    C ?? ClosureParser.GetClosures(Json.Value, cancellationToken).ToDictionary(x => new Id(x.Item1), x => x.Item2);
}

public partial interface IObjectSerializer : IDisposable;

[GenerateAutoInterface]
public sealed class ObjectSerializer : IObjectSerializer
{
  private HashSet<object> _parentObjects = new();

  private readonly IReadOnlyDictionary<Id, NodeInfo> _childCache;

  private readonly IBasePropertyGatherer _propertyGatherer;
  private readonly CancellationToken _cancellationToken;

  /// <summary>
  /// Keeps track of all detached children created during serialisation that have an applicationId (provided this serializer instance has been told to track detached children).
  /// This is currently used to cache previously converted objects and avoid their conversion if they haven't changed. See the DUI3 send bindings in rhino or another host app.
  /// </summary>
  public Dictionary<Id, ObjectReference> ObjectReferences { get; } = new();

  private readonly List<(Id, Json, Closures)> _chunks;
  private readonly Pool<List<(Id, Json, Closures)>> _chunksPool;

  private readonly List<List<DataChunk>> _chunks2 = new();
  private readonly Pool<List<DataChunk>> _chunks2Pool;

  private readonly List<List<object?>> _chunks3 = new();
  private readonly Pool<List<object?>> _chunks3Pool;

  /// <summary>
  /// Creates a new Serializer instance.
  /// </summary>
  /// <param name="cancellationToken"></param>
  public ObjectSerializer(
    IBasePropertyGatherer propertyGatherer,
    IReadOnlyDictionary<Id, NodeInfo> childCache,
    Pool<List<(Id, Json, Closures)>> chunksPool,
    Pool<List<DataChunk>> chunks2Pool,
    Pool<List<object?>> chunks3Pool,
    CancellationToken cancellationToken
  )
  {
    _propertyGatherer = propertyGatherer;
    _childCache = childCache;
    _chunksPool = chunksPool;
    _chunks2Pool = chunks2Pool;
    _chunks3Pool = chunks3Pool;
    _cancellationToken = cancellationToken;
    _chunks = chunksPool.Get();
  }

  [AutoInterfaceIgnore]
  public void Dispose()
  {
    _chunksPool.Return(_chunks);
    foreach (var c2 in _chunks2)
    {
      _chunks2Pool.Return(c2);
    }
    foreach (var c3 in _chunks3)
    {
      _chunks3Pool.Return(c3);
    }
  }

  /// <param name="baseObj">The object to serialize</param>
  /// <returns>The serialized JSON</returns>
  /// <exception cref="InvalidOperationException">The serializer is busy (already serializing an object)</exception>
  /// <exception cref="SpeckleSerializeException">Failed to extract (pre-serialize) properties from the <paramref name="baseObj"/></exception>
  public IEnumerable<(Id, Json, Closures)> Serialize(Base baseObj)
  {
    try
    {
      (Id, Json) item;
      Closures closures = [];
      try
      {
        item = SerializeBase(baseObj, true, closures, default).NotNull();
      }
      catch (Exception ex) when (!ex.IsFatal() && ex is not OperationCanceledException)
      {
        throw new SpeckleSerializeException($"Failed to extract (pre-serialize) properties from the {baseObj}", ex);
      }
      yield return (item.Item1, item.Item2, closures);
      foreach (var chunk in _chunks)
      {
        yield return chunk;
      }
    }
    finally
    {
      _parentObjects = new HashSet<object>();
    }
  }

  // `Preserialize` means transforming all objects into the final form that will appear in json, with basic .net objects
  // (primitives, lists and dictionaries with string keys)
  private void SerializeProperty(
    object? obj,
    JsonWriter writer,
    Closures closures,
    PropertyAttributeInfo propertyAttributeInfo
  )
  {
    _cancellationToken.ThrowIfCancellationRequested();

    if (obj == null)
    {
      writer.WriteNull();
      return;
    }

    switch (obj)
    {
      case double d:
        writer.WriteValue(d);
        return;
      case string d:
        writer.WriteValue(d);
        return;
      case bool d:
        writer.WriteValue(d);
        return;
      case int d:
        writer.WriteValue(d);
        return;
      case long d:
        writer.WriteValue(d);
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
      // the purpose of the send object (connector/conversion level) caching.
      case ObjectReference r:
        Dictionary<string, object?> ret = new()
        {
          ["speckle_type"] = r.speckle_type,
          ["referencedId"] = r.referencedId,
          ["__closure"] = r.closure,
        };
        closures.IncrementClosure(new(r.referencedId));
        //references can be externally provided and need to know the ids in the closure and reference here
        closures.IncrementClosures(r.closure.Empty().Select(x => new KeyValuePair<Id, int>(new Id(x.Key), x.Value)));

        SerializeProperty(ret, writer, closures, default);
        break;
      case Base b:
        var result = SerializeBase(b, false, closures, propertyAttributeInfo);
        if (result is not null)
        {
          writer.WriteRawValue(result.Value.Item2.Value);
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
            SerializeProperty(kvp.Value, writer, closures, propertyAttributeInfo);
          }
          writer.WriteEndObject();
        }
        break;
      case ICollection e:
        {
          writer.WriteStartArray();
          foreach (object? element in e)
          {
            SerializeProperty(element, writer, closures, propertyAttributeInfo);
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

  private (Id, Json)? SerializeBase(
    Base baseObj,
    bool isRequestedObject,
    Closures closures,
    PropertyAttributeInfo inheritedDetachInfo
  )
  {
    // handle circular references
    bool alreadySerialized = !_parentObjects.Add(baseObj);
    if (alreadySerialized)
    {
      return null;
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
      return SerializeDetachedBase(baseObj, closures);
    }

    //do attached
    Closures childClosures = [];
    var sb = Pools.StringBuilders.Get();
    using var writer = new StringWriter(sb);
    using var jsonWriter = SpeckleObjectSerializerPool.Instance.GetJsonTextWriter(writer);
    var id = SerializeBaseWithClosures(baseObj, jsonWriter, childClosures, isRequestedObject);
    //don't increment attached objects
    closures.MergeClosures(childClosures);
    var json = new Json(writer.ToString());
    Pools.StringBuilders.Return(sb);
    return new(id, json);
  }

  private (Id, Json)? SerializeDetachedBase(Base baseObj, Closures closures)
  {
    Closures childClosures;
    Id id;
    Json json;
    //avoid multiple serialization to get closures
    if (baseObj.id != null && _childCache.TryGetValue(new(baseObj.id), out var info))
    {
      id = new Id(baseObj.id);
      childClosures = info.GetClosures(_cancellationToken);
      json = info.Json;
      closures.IncrementClosures(childClosures);
    }
    else
    {
      childClosures = [];
      var sb = Pools.StringBuilders.Get();
      using var writer = new StringWriter(sb);
      using var jsonWriter = SpeckleObjectSerializerPool.Instance.GetJsonTextWriter(writer);
      id = SerializeBaseWithClosures(baseObj, jsonWriter, childClosures, true);
      closures.IncrementClosures(childClosures);
      json = new Json(writer.ToString());
      Pools.StringBuilders.Return(sb);
    }
    var json2 = ReferenceGenerator.CreateReference(id);
    closures.MergeClosure(id);
    // add to obj refs to return
    if (baseObj.applicationId != null) // && baseObj is not DataChunk && baseObj is not Abstract) // not needed, as data chunks will never have application ids, and abstract objs are not really used.
    {
      ObjectReferences[new(baseObj.applicationId)] = new ObjectReference()
      {
        referencedId = id.Value,
        applicationId = baseObj.applicationId,
        closure = childClosures.ToDictionary(x => x.Key.Value, x => x.Value),
      };
    }
    _chunks.Add(new(id, json, []));
    return new(id, json2);
  }

  private Id SerializeBaseWithClosures(Base baseObj, JsonWriter writer, Closures closures, bool writeClosures)
  {
    if (baseObj is not Blob)
    {
      writer = new SerializerIdWriter(writer);
    }

    writer.WriteStartObject();
    // Convert all properties
    foreach (var prop in _propertyGatherer.ExtractAllProperties(baseObj))
    {
      if (prop.PropertyAttributeInfo.JsonPropertyInfo is { NullValueHandling: NullValueHandling.Ignore })
      {
        continue;
      }

      writer.WritePropertyName(prop.Name);
      SerializeOrChunkProperty(prop.Value, writer, closures, prop.PropertyAttributeInfo);
    }

    Id id;
    if (writer is SerializerIdWriter serializerIdWriter)
    {
      (var json, writer) = serializerIdWriter.FinishIdWriter();
      id = IdGenerator.ComputeId(json);
    }
    else
    {
      id = new Id(((Blob)baseObj).id.NotNull());
    }
    writer.WritePropertyName("id");
    writer.WriteValue(id.Value);
    baseObj.id = id.Value;

    if (writeClosures && closures.Count > 0)
    {
      writer.WritePropertyName("__closure");
      writer.WriteStartObject();
      foreach (var c in closures)
      {
        writer.WritePropertyName(c.Key.Value);
        writer.WriteValue(c.Value);
      }
      writer.WriteEndObject();
    }

    writer.WriteEndObject();
    return id;
  }

  private List<object?> GetChunk()
  {
    var chunk = _chunks3Pool.Get();
    _chunks3.Add(chunk);
    return chunk;
  }

  private void SerializeOrChunkProperty(
    object? baseValue,
    JsonWriter jsonWriter,
    Closures closures,
    PropertyAttributeInfo propertyAttributeInfo
  )
  {
    if (baseValue is IEnumerable chunkableCollection && propertyAttributeInfo.IsChunkable)
    {
      List<DataChunk> chunks = _chunks2Pool.Get();
      _chunks2.Add(chunks);

      DataChunk crtChunk = new() { data = GetChunk() };

      foreach (object element in chunkableCollection)
      {
        crtChunk.data.Add(element);
        if (crtChunk.data.Count >= propertyAttributeInfo.ChunkSize)
        {
          chunks.Add(crtChunk);
          crtChunk = new DataChunk { data = GetChunk() };
        }
      }

      if (crtChunk.data.Count > 0)
      {
        chunks.Add(crtChunk);
      }

      SerializeProperty(chunks, jsonWriter, closures, new PropertyAttributeInfo(true, false, 0, null));
      return;
    }

    SerializeProperty(baseValue, jsonWriter, closures, propertyAttributeInfo);
  }
}
