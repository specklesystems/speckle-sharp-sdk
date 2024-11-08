using System.Collections.Concurrent;
using System.Reflection;
using Speckle.InterfaceGenerator;
using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Common;
using Speckle.Sdk.Helpers;
using Speckle.Sdk.Models;

namespace Speckle.Sdk.Serialisation.V2.Send;

public readonly record struct Property(string Name, object? Value, PropertyAttributeInfo PropertyAttributeInfo);

[GenerateAutoInterface]
public class BasePropertyGatherer : IBasePropertyGatherer
{
  private readonly record struct TypeProperty(PropertyInfo PropertyInfo, PropertyAttributeInfo PropertyAttributeInfo);

  private readonly ConcurrentDictionary<string, List<TypeProperty>> _typedPropertiesCache = new();

  public IEnumerable<Property> ExtractAllProperties(Base baseObj)
  {
    IReadOnlyList<TypeProperty> typedProperties = GetTypedPropertiesWithCache(baseObj);
    IReadOnlyCollection<string> dynamicProperties = baseObj.DynamicPropertyKeys;

    // Construct `allProperties`: Add typed properties
    foreach ((PropertyInfo propertyInfo, PropertyAttributeInfo detachInfo) in typedProperties)
    {
      object? baseValue = propertyInfo.GetValue(baseObj);
      yield return new(propertyInfo.Name, baseValue, detachInfo);
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

      yield return new(propName, baseValue, new PropertyAttributeInfo(isDetachable, isChunkable, chunkSize, null));
    }
  }

  // (propertyInfo, isDetachable, isChunkable, chunkSize, JsonPropertyAttribute)
  private IReadOnlyList<TypeProperty> GetTypedPropertiesWithCache(Base baseObj)
  {
    Type type = baseObj.GetType();

    if (_typedPropertiesCache.TryGetValue(type.FullName.NotNull(), out List<TypeProperty>? cached))
    {
      return cached;
    }

    var typedProperties = baseObj.GetInstanceMembers().ToList();
    List<TypeProperty> ret = new(typedProperties.Count);

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
      ret.Add(
        new(typedProperty, new PropertyAttributeInfo(isDetachable, isChunkable, chunkSize, jsonPropertyAttribute))
      );
    }

    _typedPropertiesCache[type.FullName] = ret;
    return ret;
  }
}
