using System.Collections.Immutable;
using System.Reflection;
using Speckle.Core.Common;
using Speckle.Core.Models;

namespace Speckle.Core.Serialisation;

public class CachedTypeInfo
{
  public string Key { get; private set; }
  public Type Type { get; private set; }
  public ImmutableDictionary<string, PropertyInfo> Props { get; private set; }

  public CachedTypeInfo(string key, Type type, Dictionary<string, PropertyInfo> props)
  {
    Key = key;
    Type = type;
    Props = props.ToImmutableDictionary();
  }
}

public class TypeCacheManager
{
  private readonly Type _baseType;
  private readonly Dictionary<string, CachedTypeInfo> _cachedTypes = new();

  public CachedTypeInfo? FallbackType { get; private set; }

  public TypeCacheManager(Type baseType)
  {
    _baseType = baseType;
  }

  public void EnsureCacheIsBuilt()
  {
    // POC: need a way to pick our own objects and not the DUI2 objects plus this is a touch weak :pain
    foreach (
      var assembly in AppDomain.CurrentDomain.GetAssemblies().Where(x => x.FullName.ToLower().Contains("objects"))
    )
    {
      try
      {
        var foundTypes = assembly.DefinedTypes.Where(x => x.IsSubclassOf(_baseType) && !x.IsAbstract);

        foreach (var type in foundTypes)
        {
          var typeName = type.FullName;

          // POC: I am not convinced we should have duplicates | plus the original Objects.Dll could be loaded at the same time
          if (!_cachedTypes.ContainsKey(typeName))
          {
            try
            {
              _cachedTypes.Add(typeName, new CachedTypeInfo(typeName.NotNull(), type, GetPropertyInfo(type)));
            }
            catch (TypeLoadException)
            {
              // POC: guard against loading things that cause explosions
            }
          }
        }
      }
      catch (TypeLoadException)
      {
        // POC: guard against loading things that cause explosions
      }
    }

    FallbackType = new CachedTypeInfo(_baseType.FullName.NotNull(), _baseType, GetPropertyInfo(_baseType));
  }

  public CachedTypeInfo GetType(string speckleType)
  {
    int length = speckleType.Length;
    int end = length - 1;
    int start;

    // _defaultCachedType should be created by now otherwise we should explode
    CachedTypeInfo cachedType = FallbackType.NotNull();

    do
    {
      string typeName;

      start = speckleType.LastIndexOf(':', end);
      if (start < 0)
      {
        // we didn't find a :
        typeName = speckleType.Substring(0, 1 + end);
      }
      else
      {
        typeName = speckleType.Substring(start + 1, end - start);
        end = start - 1;
      }

      if (_cachedTypes.TryGetValue(typeName, out cachedType))
      {
        return cachedType;
      }

      var lastPeriod = typeName.LastIndexOf('.');
      typeName = typeName.Insert(1 + lastPeriod, "Deprecated.");
      if (_cachedTypes.TryGetValue(typeName, out cachedType))
      {
        return cachedType;
      }
    } while (start >= 0);

    // why the hell is this moaning about it being null?
    return cachedType;
  }

  private Dictionary<string, PropertyInfo> GetPropertyInfo(Type type)
  {
    Dictionary<string, PropertyInfo> propertyMap = new();

    PropertyInfo[] properties = type.GetProperties();
    foreach (PropertyInfo prop in properties)
    {
      propertyMap[prop.Name.ToLower()] = prop;
    }

    return propertyMap;
  }
}
