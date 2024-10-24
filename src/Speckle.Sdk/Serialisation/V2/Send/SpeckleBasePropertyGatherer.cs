using System.Reflection;
using Speckle.InterfaceGenerator;
using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Common;
using Speckle.Sdk.Helpers;
using Speckle.Sdk.Models;

namespace Speckle.Sdk.Serialisation.V2.Send;

[GenerateAutoInterface]
public class SpeckleBasePropertyGatherer : ISpeckleBasePropertyGatherer
{
  private readonly Dictionary<string, List<(PropertyInfo, PropertyAttributeInfo)>> _typedPropertiesCache = new();

  public Dictionary<string, (object? value, PropertyAttributeInfo info)> ExtractAllProperties(Base baseObj)
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

      bool isDetachable = PropNameValidator.IsDetached(propName);

      int chunkSize = 1000;
      bool isChunkable = isDetachable && PropNameValidator.IsChunkable(propName, out chunkSize);

      allProperties[propName] = (baseValue, new PropertyAttributeInfo(isDetachable, isChunkable, chunkSize, null));
    }

    return allProperties;
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
}
