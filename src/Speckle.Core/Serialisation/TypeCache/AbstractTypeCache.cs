using System.Collections.Immutable;
using System.Collections.Specialized;
using System.Reflection;
using System.Runtime.Serialization;
using Speckle.Core.Common;
using Speckle.Core.Models;

namespace Speckle.Core.Serialisation.TypeCache;

internal class VersionCache(string type)
{
  public string Type { get; private set; } = type;
  public List<(Version, CachedTypeInfo)> Versions => new ();
  public CachedTypeInfo? LatestVersion;
}

public abstract class AbstractTypeCache : ITypeCache
{
  private readonly IList<Assembly> _assemblies;
  private readonly Type _baseType;
  private readonly Dictionary<string, VersionCache> _cachedTypes = new();

  public Version LoadedSchemaVersion { get; private set; }
  
  private readonly string _namespacePrefix;
  private readonly string _versionNamespacePrefix;
  
  public CachedTypeInfo? FallbackType { get; private set; }

  protected AbstractTypeCache(
                IEnumerable<Assembly> assemblies,
                Type baseType,
                Version loadedSchemaVersion,
                string namespacePrefix)
  {
    _assemblies = assemblies.ToList();
    _baseType = baseType;
    LoadedSchemaVersion = loadedSchemaVersion;
    _namespacePrefix = namespacePrefix;
    _versionNamespacePrefix = $"{_namespacePrefix}.Versions.";
    
    // POC: manually including core, not sure of the wisdom of this...
    _assemblies.Add(typeof(AbstractTypeCache).Assembly);
  }

  // could build in constructor..  but throwing from constructors generally frowned upon
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
          (string typeName, Version? version) = GetTypeNameAndVersion(type.FullName.NotNull());

          try
          {
            var typeCacheInfo = new CachedTypeInfo(
              typeName.NotNull(),
              type,
              GetPropertyInfo(type),
              GetCallbacks(type));

            CacheType(typeName, version, typeCacheInfo);
          }
          catch (TypeLoadException)
          {
            // POC: guard against loading things that cause explosions
          }
        }
      }
      catch (TypeLoadException)
      {
        // POC: guard against loading things that cause explosions
      }
    }

    // TODO: if we load versions but no latest, then this is an error
    // future incarnations may permit mutating the type
    // i.e. decorate a new type with the name of the legacy name
    
    
    // TODO: order versioned cache by their version to save sorting every version look up
    

    FallbackType = new CachedTypeInfo(
                        _baseType.FullName.NotNull(),
                        _baseType,
                        GetPropertyInfo(_baseType),
                        GetCallbacks(_baseType));
  }

  public CachedTypeInfo GetMatchedTypeOrLater(string speckleType, Version versionToMatch)
  {
    int length = speckleType.Length;
    int end = length - 1;
    int start;

    // _defaultCachedType should be created by now otherwise we should explode
    VersionCache? cachedVersions;

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

      if (_cachedTypes.TryGetValue(typeName, out cachedVersions))
      {
        return MatchOrLater(versionToMatch, cachedVersions);
      }

      var lastPeriod = typeName.LastIndexOf('.');
      typeName = typeName.Insert(1 + lastPeriod, "Deprecated.");
      if (_cachedTypes.TryGetValue(typeName, out cachedVersions))
      {
        return MatchOrLater(versionToMatch, cachedVersions);
      }
    } while (start >= 0);

    // why the hell is this moaning about it being null?
    return FallbackType.NotNull();
  }

  private CachedTypeInfo MatchOrLater(Version versionToMatch, VersionCache versionCache)
  {
    if (versionToMatch == LoadedSchemaVersion)
    {
      return versionCache.LatestVersion.NotNull();
    }
    
    // we could search or we can walk. We might be able to optomise this
    foreach ((Version version, CachedTypeInfo cachedTypeInfo) cachedVersion in versionCache.Versions)
    {
      // if it's a match or comes later, then use this
      if (cachedVersion.version >= versionToMatch)
      {
        return cachedVersion.cachedTypeInfo;
      }
    }
    
    // if we get here just use the latest version
    return versionCache.LatestVersion.NotNull();
  }

  private void CacheType(string typeName, Version version, CachedTypeInfo typeCacheInfo)
  {
    if (!_cachedTypes.TryGetValue(typeName, out VersionCache versionCache))
    {
      versionCache = new VersionCache(typeName);
      _cachedTypes[typeName] = versionCache;
    }

    // is this the latest?
    if (version == LoadedSchemaVersion)
    {
      // POC: we could be checking for prior existence 
      versionCache.LatestVersion = typeCacheInfo;
    }
    else
    {
      // we should NOT already have one of these
      if (versionCache.Versions.Any<(Version version, CachedTypeInfo cache)>(item => item.version == version))
      {
        throw new ArgumentException($"Version '{version}' already exists for cached type '{typeName}'");
      }

      versionCache.Versions.Add((version, typeCacheInfo));
    }
  }

  private (string name, Version version) GetTypeNameAndVersion(string typeName)
  {
    // is this versioned at all?
    if (typeName.StartsWith(_versionNamespacePrefix))
    {
      // this is a version, the next portion is the version number
      var nextPeriod = typeName.IndexOf('.', _versionNamespacePrefix.Length);
      if (nextPeriod == -1 || nextPeriod == typeName.Length - 1)
      {
        // period should be found but should not be the last character
        throw new ArgumentException($"Loaded type {typeName} appears to be a version but is malformed, expected a '.' and subsequent type name");
      }

      // POC: this is a sort of magic string with structure, I don't mind it because it is coming from the actual code but maybe worth considering
      // we assume the version starts V_ hence shunting things 2 chars and replacing the _ with periods
      int versionLength = nextPeriod - (_versionNamespacePrefix.Length + 2);
      string versionString =
        typeName.Substring(_versionNamespacePrefix.Length + 2, versionLength)
          .Replace('_', '.');

      string unversionedTypeName = $"{_namespacePrefix}{typeName.Substring(nextPeriod)}";

      // we have to remove the Versionsless prefix
      return (unversionedTypeName, new Version(versionString));
    }

    return (typeName, LoadedSchemaVersion);
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
