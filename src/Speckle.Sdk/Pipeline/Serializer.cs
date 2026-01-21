using System.Collections;
using System.Reflection;
using Speckle.DoubleNumerics;
using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Dependencies;
using Speckle.Sdk.Helpers;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation;

namespace Speckle.Sdk.Pipeline;

public class Serializer
{
  private readonly record struct PropertyInfo(string Name, object? Value, bool IsDetachable);

  public IEnumerable<UploadItem> Serialize(Base root)
  {
    // Special case: if root is already an ObjectReference, serialize it verbatim
    if (root is ObjectReference existingRef)
    {
      var uploadItem = ReferenceToUploadItem(existingRef);
      yield return uploadItem;
      yield break;
    }

    var detachedObjects = new List<(Id, Json, Dictionary<string, int>, Base, string)>();
    var rootClosures = new Dictionary<string, int>();

    var (rootId, rootJson) = SerializeBase(root, false, rootClosures, detachedObjects);

    var rootReference = new ObjectReference
    {
      referencedId = rootId.Value,
      applicationId = root.applicationId,
      closure = rootClosures.Count > 0 ? rootClosures : null,
    };

    yield return new UploadItem(rootId.Value, rootJson, root.speckle_type, rootReference);

    foreach (var (id, json, closures, baseObj, speckleType) in detachedObjects)
    {
      var reference = new ObjectReference
      {
        referencedId = id.Value,
        applicationId = baseObj.applicationId,
        closure = closures.Count > 0 ? closures : null,
      };

      yield return new UploadItem(id.Value, json, speckleType, reference);
    }
  }

  private IEnumerable<PropertyInfo> ExtractProperties(Base baseObj)
  {
    var typedProperties = baseObj.GetInstanceMembers();
    foreach (var prop in typedProperties)
    {
      if (prop.Name == "id" || prop.Name.StartsWith("__"))
      {
        continue;
      }

      if (prop.IsDefined(typeof(JsonIgnoreAttribute), false))
      {
        continue;
      }

      var value = prop.GetValue(baseObj);
      var isDetachable = prop.GetCustomAttribute<DetachPropertyAttribute>(true)?.Detachable ?? false;

      yield return new PropertyInfo(prop.Name, value, isDetachable);
    }

    foreach (var propName in baseObj.DynamicPropertyKeys)
    {
      if (propName.StartsWith("__"))
      {
        continue;
      }

      var value = baseObj[propName];

#pragma warning disable CA1866
      var isDetachable = propName.StartsWith("@");
#pragma warning restore CA1866

      yield return new PropertyInfo(propName, value, isDetachable);
    }
  }

  private (Id, Json) SerializeBase(
    Base baseObj,
    bool forceDetach,
    Dictionary<string, int> closures,
    List<(Id, Json, Dictionary<string, int>, Base, string)> detachedObjects
  )
  {
    var childClosures = new Dictionary<string, int>();

    var sb = Pools.StringBuilders.Get();
    try
    {
      using var stringWriter = new StringWriter(sb);
      using var jsonWriter = new JsonTextWriter(stringWriter);
      using var idWriter = new SerializerIdWriter(jsonWriter);

      idWriter.WriteStartObject();

      foreach (var prop in ExtractProperties(baseObj))
      {
        idWriter.WritePropertyName(prop.Name);
        SerializeValue(prop.Value, idWriter, prop.IsDetachable, childClosures, detachedObjects);
      }

      var (jsonForId, finalWriter) = idWriter.FinishIdWriter();
      var id = IdGenerator.ComputeId(jsonForId);

      finalWriter.WritePropertyName("id");
      finalWriter.WriteValue(id.Value);

      baseObj.id = id.Value;

      if ((forceDetach || childClosures.Count > 0) && childClosures.Count > 0)
      {
        finalWriter.WritePropertyName("__closure");
        finalWriter.WriteStartObject();
        foreach (var kvp in childClosures)
        {
          finalWriter.WritePropertyName(kvp.Key);
          finalWriter.WriteValue(kvp.Value);
        }
        finalWriter.WriteEndObject();

        foreach (var kvp in childClosures)
        {
          closures[kvp.Key] = closures.TryGetValue(kvp.Key, out var existing) ? existing + kvp.Value : kvp.Value;
        }
      }

      finalWriter.WriteEndObject();
      finalWriter.Flush();

      var json = new Json(stringWriter.ToString());
      return (id, json);
    }
    finally
    {
      Pools.StringBuilders.Return(sb);
    }
  }

  private void SerializeValue(
    object? value,
    JsonWriter writer,
    bool isDetachable,
    Dictionary<string, int> closures,
    List<(Id, Json, Dictionary<string, int>, Base, string)> detachedObjects
  )
  {
    if (value == null)
    {
      writer.WriteNull();
      return;
    }

    switch (value)
    {
      case string s:
        writer.WriteValue(s);
        return;
      case int i:
        writer.WriteValue(i);
        return;
      case long l:
        writer.WriteValue(l);
        return;
      case double d:
        writer.WriteValue(d);
        return;
      case float f:
        writer.WriteValue(f);
        return;
      case bool b:
        writer.WriteValue(b);
        return;
      case Enum e:
        writer.WriteValue((int)(object)e);
        return;
      case Guid g:
        writer.WriteValue(g.ToString());
        return;
      case DateTime dt:
        writer.WriteValue(dt.ToString("o"));
        return;

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
        return;
    }

    // Handle ObjectReference before Base (since ObjectReference extends Base)
    // This prevents double-serialization and properly propagates closures
    if (value is ObjectReference objRef)
    {
      writer.WriteStartObject();
      writer.WritePropertyName("speckle_type");
      writer.WriteValue("reference");
      writer.WritePropertyName("referencedId");
      writer.WriteValue(objRef.referencedId);
      writer.WriteEndObject();

      // Propagate closure: add the referenced ID
      closures[objRef.referencedId] = closures.TryGetValue(objRef.referencedId, out var existing) ? existing + 1 : 1;

      // Propagate nested closures from the ObjectReference.closure dictionary
      if (objRef.closure != null)
      {
        foreach (var kvp in objRef.closure)
        {
          closures[kvp.Key] = closures.TryGetValue(kvp.Key, out var existingDepth)
            ? existingDepth + kvp.Value
            : kvp.Value;
        }
      }

      return;
    }

    if (value is Base baseObj)
    {
      if (isDetachable)
      {
        var childClosures = new Dictionary<string, int>();
        var (childId, childJson) = SerializeBase(baseObj, true, childClosures, detachedObjects);

        detachedObjects.Add((childId, childJson, childClosures, baseObj, baseObj.speckle_type));

        writer.WriteStartObject();
        writer.WritePropertyName("speckle_type");
        writer.WriteValue("reference");
        writer.WritePropertyName("referencedId");
        writer.WriteValue(childId.Value);
        writer.WriteEndObject();

        closures[childId.Value] = closures.TryGetValue(childId.Value, out var existing) ? existing + 1 : 1;

        foreach (var kvp in childClosures)
        {
          closures[kvp.Key] = closures.TryGetValue(kvp.Key, out var existingDepth)
            ? existingDepth + kvp.Value
            : kvp.Value;
        }
      }
      else
      {
        var inlineClosures = new Dictionary<string, int>();
        var (_, inlineJson) = SerializeBase(baseObj, false, inlineClosures, detachedObjects);

        writer.WriteRawValue(inlineJson.Value);

        foreach (var kvp in inlineClosures)
        {
          closures[kvp.Key] = closures.TryGetValue(kvp.Key, out var existingDepth)
            ? existingDepth + kvp.Value
            : kvp.Value;
        }
      }
      return;
    }

    if (value is IDictionary dict)
    {
      writer.WriteStartObject();
      foreach (DictionaryEntry kvp in dict)
      {
        if (kvp.Key is not string key)
        {
          throw new ArgumentException("Dictionary keys must be strings");
        }

        writer.WritePropertyName(key);
        SerializeValue(kvp.Value, writer, false, closures, detachedObjects);
      }
      writer.WriteEndObject();
      return;
    }

    if (value is IEnumerable enumerable and not string)
    {
      writer.WriteStartArray();
      foreach (var item in enumerable)
      {
        SerializeValue(item, writer, isDetachable, closures, detachedObjects);
      }
      writer.WriteEndArray();
      return;
    }

    writer.WriteValue(value.ToString());
  }

  private UploadItem ReferenceToUploadItem(ObjectReference existingRef)
  {
    var sb = Pools.StringBuilders.Get();
    try
    {
      using var stringWriter = new StringWriter(sb);
      using var jsonWriter = new JsonTextWriter(stringWriter);

      jsonWriter.WriteStartObject();
      jsonWriter.WritePropertyName("speckle_type");
      jsonWriter.WriteValue("reference");
      jsonWriter.WritePropertyName("referencedId");
      jsonWriter.WriteValue(existingRef.referencedId);
      jsonWriter.WritePropertyName("__closure");

      if (existingRef.closure != null && existingRef.closure.Count > 0)
      {
        jsonWriter.WriteStartObject();
        foreach (var kvp in existingRef.closure)
        {
          jsonWriter.WritePropertyName(kvp.Key);
          jsonWriter.WriteValue(kvp.Value);
        }
        jsonWriter.WriteEndObject();
      }
      else
      {
        jsonWriter.WriteNull();
      }

      jsonWriter.WriteEndObject();
      jsonWriter.Flush();

      var refJson = new Json(stringWriter.ToString());

      return new UploadItem(
        existingRef.referencedId,
        refJson,
        existingRef.speckle_type,
        existingRef // Pass through the original ObjectReference
      );
    }
    finally
    {
      Pools.StringBuilders.Return(sb);
    }
  }
}
