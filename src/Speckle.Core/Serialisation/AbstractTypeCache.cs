using System.Collections.Immutable;
using System.Reflection;
using Speckle.Core.Common;
using Speckle.Core.Models;

namespace Speckle.Core.Serialisation;

public class CachedType
{
  public string Key { get; private set; }
  public Type Type { get; private set; }
  public ImmutableDictionary<string, PropertyInfo> Props { get; private set; }

  public CachedType(string key, Type type, Dictionary<string, PropertyInfo> props)
  {
    Key = key;
    Type = type;
    Props = props.ToImmutableDictionary();
  }
}

public abstract class AbstractTypeCache
{
  private readonly Assembly _assemblyToCache;
  private readonly Type _baseType;
  private readonly Dictionary<string, CachedType> _cachedTypes = new();

  private CachedType? _defaultCachedType = null;

  protected AbstractTypeCache(Assembly assemblyToCache, Type baseType)
  {
    _assemblyToCache = assemblyToCache;
    _baseType = baseType;
  }

  public void EnsureCacheIsBuilt()
  {
    var foundTypes = _assemblyToCache.DefinedTypes.Where(x => x.IsSubclassOf(_baseType));

    foreach (var type in foundTypes)
    {
      _cachedTypes.Add(type.FullName, new CachedType(type.FullName.NotNull(), type, GetPropertyInfo(type)));
    }

    _defaultCachedType = new CachedType(_baseType.FullName.NotNull(), _baseType, GetPropertyInfo(_baseType));
  }

  public CachedType GetType(string speckleType)
  {
    int length = speckleType.Length;
    int end = length - 1;
    int start;

    // _defaultCachedType should be created by now otherwise we should explode
    CachedType cachedType = _defaultCachedType.NotNull();

    do
    {
      string typeName;

      start = speckleType.LastIndexOf(':', 0, end);
      if (start < 0)
      {
        // we didn't find a :
        typeName = speckleType.Substring(0, end);
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
      typeName = speckleType.Insert(lastPeriod, "Deprecated.");
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

public class SpeckleBaseTypeCache : AbstractTypeCache
{
  public SpeckleBaseTypeCache()
    : base(Assembly.GetExecutingAssembly(), typeof(Base)) { }
}
