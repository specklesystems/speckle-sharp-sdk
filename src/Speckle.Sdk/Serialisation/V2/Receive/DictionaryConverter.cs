using System.Reflection;
using Speckle.Sdk.Common;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation.Utilities;

namespace Speckle.Sdk.Serialisation.V2.Receive;

public static class DictionaryConverter
{
  /// <summary>
  /// Property that describes the type of the object.
  /// </summary>
  public const string TYPE_DISCRIMINATOR = nameof(Base.speckle_type);
  private static readonly object?[] s_invokeNull = [null];

  public static string? BlobStorageFolder { get; set; }

  public static Base Dict2Base(Dictionary<string, object?> dictObj, bool skipInvalidConverts)
  {
    string typeName = (string)dictObj[TYPE_DISCRIMINATOR].NotNull();
    Type type = TypeLoader.GetType(typeName);
    Base baseObj = (Base)Activator.CreateInstance(type).NotNull();

    dictObj.Remove(TYPE_DISCRIMINATOR);
    dictObj.Remove("__closure");

    var staticProperties = TypeCache.GetTypeProperties(typeName);
    foreach (var entry in dictObj)
    {
      if (staticProperties.TryGetValue(entry.Key, out PropertyInfo? value) && value.CanWrite)
      {
        if (entry.Value == null)
        {
          // Check for JsonProperty(NullValueHandling = NullValueHandling.Ignore) attribute
          Speckle.Newtonsoft.Json.JsonPropertyAttribute? attr = TypeLoader.GetJsonPropertyAttribute(value);
          if (attr is { NullValueHandling: Speckle.Newtonsoft.Json.NullValueHandling.Ignore })
          {
            continue;
          }
        }

        Type targetValueType = value.PropertyType;
        bool conversionOk = ValueConverter.ConvertValue(
          targetValueType,
          entry.Value,
          skipInvalidConverts,
          out object? convertedValue
        );
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

    var onDeserializedCallbacks = TypeCache.GetOnDeserializedCallbacks(typeName);
    foreach (MethodInfo onDeserialized in onDeserializedCallbacks)
    {
      onDeserialized.Invoke(baseObj, s_invokeNull);
    }

    return baseObj;
  }
}
