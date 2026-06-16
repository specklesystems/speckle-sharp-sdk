using System.Collections;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using Speckle.DoubleNumerics;
using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Dependencies;
using Speckle.Sdk.Helpers;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation;

namespace Speckle.Sdk.Pipelines.Send;

/// <summary>
/// Speckle 4.0 serializer — a closure-free variant of <see cref="Serializer"/>.
/// Produces the same <see cref="UploadItem"/> shape (id, JSON, speckle_type,
/// references, detached children) but generates NO <c>__closure</c>: it is
/// neither accumulated nor written. Topology in 4.0 comes from collections /
/// proxies + applicationId, not closure dependency maps. The original
/// <see cref="Serializer"/> is left untouched for the legacy/closure path.
/// </summary>
internal sealed class SerializerV2
{
  private readonly record struct PropertyInfo(string Name, object? Value, bool IsDetachable);

  public IEnumerable<UploadItem> Serialize(Base root)
  {
    // Special case: if root is already an ObjectReference, serialize it verbatim
    if (root is ObjectReference existingRef)
    {
      yield return ReferenceToUploadItem(existingRef);
      yield break;
    }

    var detachedObjects = new List<(Id, Json, Base, string)>();

    var (rootId, rootJson) = SerializeBase(root, detachedObjects);

    yield return new UploadItem(
      rootId.Value,
      rootJson,
      root.speckle_type,
      new ObjectReference { referencedId = rootId.Value, applicationId = root.applicationId, closure = null }
    );

    foreach (var (id, json, baseObj, speckleType) in detachedObjects)
    {
      yield return new UploadItem(
        id.Value,
        json,
        speckleType,
        new ObjectReference { referencedId = id.Value, applicationId = baseObj.applicationId, closure = null }
      );
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

  private (Id, Json) SerializeBase(Base baseObj, List<(Id, Json, Base, string)> detachedObjects)
  {
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
        SerializeValue(prop.Value, idWriter, prop.IsDetachable, detachedObjects);
      }

      var (jsonForId, finalWriter) = idWriter.FinishIdWriter();
      var id = IdGenerator.ComputeId(jsonForId);

      finalWriter.WritePropertyName("id");
      finalWriter.WriteValue(id.Value);

      baseObj.id = id.Value;

      // 4.0: no __closure written.
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
    List<(Id, Json, Base, string)> detachedObjects
  )
  {
    switch (value)
    {
      case Enum:
        writer.WriteValue((int)value);
        return;
      case Guid g:
        writer.WriteValue(g.ToString());
        return;
      case Color c:
        writer.WriteValue(c.ToArgb());
        return;
      case DateTime dt:
        writer.WriteValue(dt.ToString("o", CultureInfo.InvariantCulture));
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
      // ObjectReference before Base (ObjectReference extends Base). No closure propagation in 4.0.
      case ObjectReference objRef:
        writer.WriteStartObject();
        writer.WritePropertyName("speckle_type");
        writer.WriteValue("reference");
        writer.WritePropertyName("referencedId");
        writer.WriteValue(objRef.referencedId);
        writer.WriteEndObject();
        return;
      case Base baseObj:
      {
        if (isDetachable)
        {
          var (childId, childJson) = SerializeBase(baseObj, detachedObjects);
          detachedObjects.Add((childId, childJson, baseObj, baseObj.speckle_type));

          writer.WriteStartObject();
          writer.WritePropertyName("speckle_type");
          writer.WriteValue("reference");
          writer.WritePropertyName("referencedId");
          writer.WriteValue(childId.Value);
          writer.WriteEndObject();
        }
        else
        {
          var (_, inlineJson) = SerializeBase(baseObj, detachedObjects);
          writer.WriteRawValue(inlineJson.Value);
        }
        return;
      }
      case IDictionary dict:
      {
        writer.WriteStartObject();
        foreach (DictionaryEntry kvp in dict)
        {
          if (kvp.Key is not string key)
          {
            throw new ArgumentException("Dictionary keys must be strings", nameof(value));
          }

          writer.WritePropertyName(key);
          SerializeValue(kvp.Value, writer, false, detachedObjects);
        }
        writer.WriteEndObject();
        return;
      }
      case ICollection collection:
      {
        writer.WriteStartArray();
        foreach (var item in collection)
        {
          SerializeValue(item, writer, isDetachable, detachedObjects);
        }
        writer.WriteEndArray();
        return;
      }
      default:
        // primitives and `null`; throws JsonWriterException if unsupported
        writer.WriteValue(value);
        return;
    }
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
      jsonWriter.WriteEndObject();
      jsonWriter.Flush();

      var refJson = new Json(stringWriter.ToString());

      return new UploadItem(existingRef.referencedId, refJson, existingRef.speckle_type, existingRef);
    }
    finally
    {
      Pools.StringBuilders.Return(sb);
    }
  }
}
