using System.Reflection;
using System.Runtime.Serialization;
using Speckle.Core.Common;
using Speckle.Core.Reflection;

namespace Speckle.Core.Serialisation.TypeCache;

// POC: could move
internal class VersionCache
{
  public string Type { get; private set; }
  public List<(Version, CachedTypeInfo)> Versions { get; private set; } = new();
  public CachedTypeInfo? LatestVersion;

  public VersionCache(string type)
  {
    Type = type;
  }

  public void SortVersions()
  {
    // for some reason I can't get the tuple deconstructed (but it IS rather late)
    Versions = Versions.OrderBy(v => v.Item1).ToList();
  }
}

public abstract class AbstractTypeCache : ITypeCache
{
  private readonly IList<Assembly> _assemblies;
  private readonly Type _baseType;
  private readonly Dictionary<string, VersionCache> _cachedTypes = new();

  private bool _cacheBuilt = false;

  public Version LoadedSchemaVersion { get; private set; }
  
  private readonly ITypeFinder _typeFinder;
  private readonly string _namespacePrefix;
  private readonly string _versionNamespacePrefix;
  
  public CachedTypeInfo? FallbackType { get; private set; }

  protected AbstractTypeCache(
                IEnumerable<Assembly> assemblies,
                Type baseType,
                Version loadedSchemaVersion,
                string namespacePrefix,
                ITypeFinder typeFinder)
  {
    _assemblies = assemblies.ToList();
    _baseType = baseType;
    LoadedSchemaVersion = loadedSchemaVersion;
    
    // FUTURE: we may need to associate the prefix with each assembly as the prefix may change
    // better yet detach the namespaces & assemblies from the type naming
    _namespacePrefix = namespacePrefix;
    _versionNamespacePrefix = $"{_namespacePrefix}.Versions.";
    _typeFinder = typeFinder;
    
    // POC: manually including core, not sure of the wisdom of this...
    _assemblies.Add(typeof(AbstractTypeCache).Assembly);
  }

  // could build in constructor..  but throwing from constructors generally frowned upon
  public void EnsureCacheIsBuilt()
  {
    if (_cacheBuilt)
    {
      return;
    }

    _cacheBuilt = true;

    foreach (Type type in _typeFinder.GetTypesWhereSubclassOf(_assemblies, _baseType))
    {
      try
      {
        (string typeName, Version? version) = GetTypeNameAndVersion(type.FullName.NotNull());

        var typeCacheInfo = new CachedTypeInfo(
          typeName.NotNull(),
          type,
          GetPropertyInfo(type),
          GetCallbacks(type));

        CacheType(typeName, version, typeCacheInfo);
      }
      // POC: right one? more?
      catch (ReflectionTypeLoadException)
      {
        // POC: guard against loading things that cause explosions
      }      
    }

    // future incarnations may permit mutating the type
    // so the return type is something other than base, they may also allow moving namespaces
    // this is not currently possible because of the way the namespace has been used historically and how we are using it here
    // we can probably tweak this to allow for namespace remapping - it would be nice if things began with Speckle :)
    // object version may also be able to retype things...
    foreach (var typeVersions in _cachedTypes.Values)
    {
      // if (typeVersions.LatestVersion == null)
      // {
      //   // we cannot have non-matching types atm
      //   // .i.e. a versioned Objects.Versions.V_1_2.0.Wall must have a corresponding Objects.Wall
      //   // I imagine it would be possible to add some annotation somewhere to allow for this but now is not the time...
      //   var versionNames = string.Join(",", typeVersions.Versions
      //     .Select((v, typeCache) => v.Item1.ToString())).ToList();
      //   throw new ArgumentException(
      //     $"The type {typeVersions.Type} has no latest - we have the following versions of this type: '{versionNames}'");
      // }
      
      // sort the versions
      typeVersions.SortVersions();
    }

    FallbackType = new CachedTypeInfo(
                        _baseType.FullName.NotNull(),
                        _baseType,
                        GetPropertyInfo(_baseType),
                        GetCallbacks(_baseType));
  }

  public (Version version, CachedTypeInfo cachedTypeInfo) GetMatchedTypeOrLater(string speckleType, Version versionToMatch)
  {
    int length = speckleType.Length;
    int end = length - 1;
    int start;

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

    // FallbackType should exist
    return (LoadedSchemaVersion, FallbackType.NotNull());
  }

  private (Version version, CachedTypeInfo cachedTypeInfo) MatchOrLater(Version versionToMatch, VersionCache versionCache)
  {
    if (versionToMatch == LoadedSchemaVersion)
    {
      return (LoadedSchemaVersion, versionCache.LatestVersion.NotNull());
    }
    
    // we could search or we can walk. We might be able to optomise this
    foreach ((Version version, CachedTypeInfo cachedTypeInfo) cachedVersion in versionCache.Versions)
    {
      // if it's a match or comes later, then use this
      if (cachedVersion.version >= versionToMatch)
      {
        return (cachedVersion.version, cachedVersion.cachedTypeInfo);
      }
    }
    
    // if we get here just use the latest version
    return (LoadedSchemaVersion, versionCache.LatestVersion.NotNull());
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
