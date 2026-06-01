using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Speckle.Sdk.Common;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation.Utilities;

namespace Speckle.Sdk.Pipelines.Receive.JsonConverters;

public sealed class SpeckleObjectJsonConverter(PackFileManager packFileManager) : JsonConverter<Base>
{
  public override Base? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
  {
    using var doc = JsonDocument.ParseValue(ref reader);
    var root = doc.RootElement;

    string speckleType = root.GetProperty("speckle_type").GetString().NotNull("No speckle type");

    // Handle references
    if (speckleType == "reference")
    {
      return DereferenceDetachedReference(root);
    }

    var obj = Materialize(root, speckleType, options);

    return obj;
  }

  private Base DereferenceDetachedReference(JsonElement referenceObject)
  {
    var referenceId = referenceObject.GetProperty(nameof(ObjectReference.referencedId)).GetString().NotNull();

    return packFileManager.GetObject(referenceId);
    // return map.TryGetValue(referenceId, out var referenced)
    //   ? referenced
    //   : throw new JsonException($"Unresolved reference '{referenceId}'");
  }

  private Base Materialize(JsonElement element, string speckleType, JsonSerializerOptions options)
  {
    var runtimeType = TypeLoader.GetAtomicType(speckleType);
    var baseObj = (Base)Activator.CreateInstance(runtimeType).NotNull($"Failed to create {runtimeType}");

    var typedProperties = TypeCache.GetTypeProperties(speckleType);

    foreach (var jsonProp in element.EnumerateObject())
    {
      if (jsonProp.NameEquals("__closure") || jsonProp.NameEquals("speckle_type"))
      {
        continue;
      }

      if (!element.TryGetProperty(jsonProp.Name, out JsonElement propElement))
      {
        continue;
      }

      if (typedProperties.TryGetValue(jsonProp.Name, out PropertyInfo? prop) && prop.CanWrite)
      {
        object? value;

        if (typeof(Base).IsAssignableFrom(prop.PropertyType))
        {
          // nested Speckle object
          value = JsonSerializer.Deserialize<Base>(propElement.GetRawText(), options);
        }
        else
        {
#if NET5_0_OR_GREATER

          value = propElement.Deserialize(prop.PropertyType, options);
#else
          value = JsonSerializer.Deserialize(propElement.GetRawText(), prop.PropertyType, options);
#endif
        }

        prop.SetValue(baseObj, value);
      }
      else
      {
        // No writable property with this name, set dynamically
        //TODO: test if I can just write to the properties dictionary directly...
        var dynamicValue = ReadDynamicProperty(jsonProp.Value, options);
        CallSiteCache.SetValue(jsonProp.Name, baseObj, dynamicValue);
      }
    }

    return baseObj;
  }

  private object? ReadDynamicProperty(JsonElement element, JsonSerializerOptions options)
  {
    return element.ValueKind switch
    {
      JsonValueKind.Undefined or JsonValueKind.Null => null,
      JsonValueKind.Object => ReadDynamicObjectAsync(element, options),
      JsonValueKind.Array => ReadReadDynamicArrayAsync(element, options),
      JsonValueKind.String => element.GetString(),
      JsonValueKind.Number => element.GetDouble(),
      JsonValueKind.True => true,
      JsonValueKind.False => false,
      _ => throw new ArgumentOutOfRangeException(nameof(element)),
    };
  }

  private List<object?> ReadReadDynamicArrayAsync(JsonElement array, JsonSerializerOptions options)
  {
    List<object?> retList = new();
    foreach (var element in array.EnumerateArray())
    {
      object? convertedValue = ReadDynamicProperty(element, options);
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

  private object? ReadDynamicObjectAsync(JsonElement element, JsonSerializerOptions options)
  {
    if (element.TryGetProperty("speckle_type", out JsonElement speckleTypeProp))
    {
      string speckleType = speckleTypeProp.GetString().NotNull("Expected speckle_type to be non-nullable");
      if (speckleType == "reference")
      {
        return DereferenceDetachedReference(element);
      }

      return JsonSerializer.Deserialize<Base>(element.GetRawText(), options);
    }

    //Deserialize as dictionary
    return JsonSerializer.Deserialize<Dictionary<string, object?>>(element.GetRawText(), options);
  }

  public override void Write(Utf8JsonWriter writer, Base value, JsonSerializerOptions options) =>
    throw new NotSupportedException();
}
