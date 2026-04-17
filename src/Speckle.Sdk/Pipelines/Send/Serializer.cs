using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Text;
using System.Text.Json;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation;
using Matrix4x4 = Speckle.DoubleNumerics.Matrix4x4;

namespace Speckle.Sdk.Pipelines.Send;

/// <summary>
/// Another serializer, cleaner and meaner. Provides methods for serializing Speckle objects into a format suitable for upload or storage.
/// This class handles the conversion of <see cref="Speckle.Sdk.Models.Base"/> and its derivatives
/// into serialized JSON structures along with associated metadata, closures, and references.
/// <para>Any reference objects coming through are being "passed through" serialized - they do not get double encoded.</para>
/// </summary>
internal sealed class Serializer
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

    var detachedObjects = new List<(Id, EfficientJson, Dictionary<string, int>, Base, string)>();
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

      if (prop.IsDefined(typeof(Speckle.Newtonsoft.Json.JsonIgnoreAttribute), false))
      {
        continue;
      }

      var value = prop.GetValue(baseObj);
      var isDetachable = prop.IsDefined(typeof(DetachPropertyAttribute), true);

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

  private (Id, EfficientJson) SerializeBase(
    Base baseObj,
    bool forceDetach,
    Dictionary<string, int> closures,
    List<(Id, EfficientJson, Dictionary<string, int>, Base, string)> detachedObjects
  )
  {
    var childClosures = new Dictionary<string, int>();

    var efficientJson = new EfficientJson();
    using var jsonWriter = new Utf8JsonWriter(efficientJson.Buffer);

    jsonWriter.WriteStartObject();

    foreach (var prop in ExtractProperties(baseObj))
    {
      jsonWriter.WritePropertyName(prop.Name);
      SerializeValue(prop.Value, jsonWriter, prop.IsDetachable, childClosures, detachedObjects);
    }

    jsonWriter.Flush();

#if NET6_0_OR_GREATER
    // We want to hash the json string now to calculate the id
    // We don't want to allocate a separate buffer for it, as this wouldn't be memory efficient
    // It's also (debatably) important that the bytes we hash are the full json object (minus id and closures obviously)
    // For this, we are manually writing the closing } bracket without calling Buffer.Advance
    // Such that, the buffer can continue to write the id, and closures later in this function.
    var bytes = efficientJson.Buffer.GetSpan(efficientJson.WrittenCount + 1);
    bytes[^1] = (byte)'}';
    string id = IdGenerator.ComputeId(bytes);
#else
    efficientJson.CheckAndResizeBuffer(efficientJson.WrittenCount + 1);
    var bytes = efficientJson.GetInternalBuffer();
    bytes[efficientJson.WrittenCount] = (byte)'}';
    string id = IdGenerator.ComputeId(bytes, 0, efficientJson.WrittenCount);
#endif
    string str = Encoding.UTF8.GetString(bytes);
    jsonWriter.WriteString("id", id);

    baseObj.id = id;

    if ((forceDetach || childClosures.Count > 0) && childClosures.Count > 0)
    {
      jsonWriter.WritePropertyName("__closure");
      jsonWriter.WriteStartObject();
      foreach (var kvp in childClosures)
      {
        jsonWriter.WriteNumber(kvp.Key, kvp.Value);
      }
      jsonWriter.WriteEndObject();

      foreach (var kvp in childClosures)
      {
        closures[kvp.Key] = closures.TryGetValue(kvp.Key, out var existing) ? existing + kvp.Value : kvp.Value;
      }
    }

    jsonWriter.WriteEndObject();
    jsonWriter.Flush();
    return (new(id), efficientJson);
  }

  private void SerializeValue(
    object? value,
    Utf8JsonWriter writer,
    bool isDetachable,
    Dictionary<string, int> closures,
    List<(Id, EfficientJson, Dictionary<string, int>, Base, string)> detachedObjects
  )
  {
    switch (value)
    {
      case null:
        writer.WriteNullValue();
        return;
      case string v:
        writer.WriteStringValue(v);
        return;
      case short i:
        writer.WriteNumberValue(i);
        return;
      case ushort i:
        writer.WriteNumberValue(i);
        return;
      case int i:
        writer.WriteNumberValue(i);
        return;
      case uint i:
        writer.WriteNumberValue(i);
        return;
      case long i:
        writer.WriteNumberValue(i);
        return;
      case ulong i:
        writer.WriteNumberValue(i);
        return;
      case bool b:
        writer.WriteBooleanValue(b);
        return;
      case float f:
        writer.WriteNumberValue(f);
        return;
      case double f:
        writer.WriteNumberValue(f);
        return;
      case decimal d:
        writer.WriteNumberValue(d);
        return;
      case Enum:
        writer.WriteNumberValue((int)value);
        return;
      case Color c:
        writer.WriteNumberValue(c.ToArgb());
        return;
      case Matrix4x4 md:
        writer.WriteStartArray();
        writer.WriteNumberValue(md.M11);
        writer.WriteNumberValue(md.M12);
        writer.WriteNumberValue(md.M13);
        writer.WriteNumberValue(md.M14);
        writer.WriteNumberValue(md.M21);
        writer.WriteNumberValue(md.M22);
        writer.WriteNumberValue(md.M23);
        writer.WriteNumberValue(md.M24);
        writer.WriteNumberValue(md.M31);
        writer.WriteNumberValue(md.M32);
        writer.WriteNumberValue(md.M33);
        writer.WriteNumberValue(md.M34);
        writer.WriteNumberValue(md.M41);
        writer.WriteNumberValue(md.M42);
        writer.WriteNumberValue(md.M43);
        writer.WriteNumberValue(md.M44);
        writer.WriteEndArray();
        return;
      // Handle ObjectReference before Base (since ObjectReference extends Base)
      // This prevents double-serialization and properly propagates closures
      case ObjectReference objRef:
      {
        writer.WriteStartObject();
        writer.WriteString("speckle_type", "reference");
        writer.WriteString("referencedId", objRef.referencedId);
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
      case Base baseObj:
      {
        if (isDetachable)
        {
          var childClosures = new Dictionary<string, int>();
          var (childId, childJson) = SerializeBase(baseObj, true, childClosures, detachedObjects);

          detachedObjects.Add((childId, childJson, childClosures, baseObj, baseObj.speckle_type));

          writer.WriteStartObject();
          writer.WriteString("speckle_type", "reference");
          writer.WriteString("referencedId", childId.Value);
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

          writer.WriteRawValue(inlineJson.WrittenSpan);

          foreach (var kvp in inlineClosures)
          {
            closures[kvp.Key] = closures.TryGetValue(kvp.Key, out var existingDepth)
              ? existingDepth + kvp.Value
              : kvp.Value;
          }
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
          SerializeValue(kvp.Value, writer, false, closures, detachedObjects);
        }
        writer.WriteEndObject();
        return;
      }
      case ICollection collection:
      {
        writer.WriteStartArray();
        foreach (var item in collection)
        {
          SerializeValue(item, writer, isDetachable, closures, detachedObjects);
        }
        writer.WriteEndArray();
        return;
      }
      default:
        // This case will handle primitives and `null`
        // Will throw JsonWriterException if not supported
        throw new ArgumentOutOfRangeException(nameof(value), $"Unsupported type {value.GetType()}");
    }
  }

  [SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Justification = "EfficientJson IDisposable is returned via UploadItem"
  )]
  private UploadItem ReferenceToUploadItem(ObjectReference existingRef)
  {
    var refJson = new EfficientJson();
    using var jsonWriter = new Utf8JsonWriter(refJson.Buffer);

    jsonWriter.WriteStartObject();
    jsonWriter.WriteString("speckle_type", "reference");
    jsonWriter.WriteString("referencedId", existingRef.referencedId);
    jsonWriter.WritePropertyName("__closure");

    if (existingRef.closure != null && existingRef.closure.Count > 0)
    {
      jsonWriter.WriteStartObject();
      foreach (var kvp in existingRef.closure)
      {
        jsonWriter.WritePropertyName(kvp.Key);
        jsonWriter.WriteNumberValue(kvp.Value);
      }
      jsonWriter.WriteEndObject();
    }
    else
    {
      jsonWriter.WriteNullValue();
    }

    jsonWriter.WriteEndObject();
    jsonWriter.Flush();

    return new UploadItem(
      existingRef.referencedId,
      refJson,
      existingRef.speckle_type,
      existingRef // Pass through the original ObjectReference
    );
  }
}
