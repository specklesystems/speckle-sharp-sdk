using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.Serialization;
using Speckle.Core.Common;
using Speckle.Core.Models;

namespace Speckle.Core.Serialisation.TypeCache;

public abstract class AbstractTypeCache : ITypeCache
{
  private readonly IList<Assembly> _assemblies;
  private readonly Type _baseType;
  private readonly Dictionary<string, CachedTypeInfo> _cachedTypes = new();

  public CachedTypeInfo? FallbackType { get; private set; }

  protected AbstractTypeCache(IEnumerable<Assembly> assemblies, Type baseType)
  {
    _assemblies = assemblies.ToList();
    _baseType = baseType;
    
    // POC: manually including core, not sure of the wisdom of this...
    _assemblies.Add(typeof(AbstractTypeCache).Assembly);
  }

  public void EnsureCacheIsBuilt()
  {
    // POC: need a way to pick our own objects and not the DUI2 objects plus this is a touch weak :pain
    foreach (var assembly in _assemblies)
    {
      try
      {
        var foundTypes = assembly.GetTypes().Where(x => x.IsSubclassOf(_baseType) && !x.IsAbstract);

        foreach (var type in foundTypes)
        {
          var typeName = type.FullName;

          // POC: I am not convinced we should have duplicates | plus the original Objects.Dll could be loaded at the same time
          if (!_cachedTypes.ContainsKey(typeName))
          {
            try
            {
              _cachedTypes.Add(
                            typeName,
                            new CachedTypeInfo(
                                typeName.NotNull(),
                                type,
                                GetPropertyInfo(type),
                                GetCallbacks(type)));
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

    FallbackType = new CachedTypeInfo(
                        _baseType.FullName.NotNull(),
                        _baseType,
                        GetPropertyInfo(_baseType),
                        GetCallbacks(_baseType));
  }

  public CachedTypeInfo GetType(string speckleType)
  {
    int length = speckleType.Length;
    int end = length - 1;
    int start;

    // _defaultCachedType should be created by now otherwise we should explode
    CachedTypeInfo? cachedType;

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
        return cachedType.NotNull();
      }

      var lastPeriod = typeName.LastIndexOf('.');
      typeName = typeName.Insert(1 + lastPeriod, "Deprecated.");
      if (_cachedTypes.TryGetValue(typeName, out cachedType))
      {
        return cachedType.NotNull();
      }
    } while (start >= 0);

    // why the hell is this moaning about it being null?
    return FallbackType.NotNull();
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

  private List<MethodInfo> GetCallbacks(Type type)
  {
    List<MethodInfo> callbacks = new();

    MethodInfo[] methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    foreach (MethodInfo method in methods)
    {
      List<OnDeserializedAttribute> onDeserializedAttributes = method
        .GetCustomAttributes<OnDeserializedAttribute>(true)
        .ToList();
      if (onDeserializedAttributes.Count > 0)
      {
        callbacks.Add(method);
      }
    }

    return callbacks;
  }
}
