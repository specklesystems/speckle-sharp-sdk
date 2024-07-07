namespace Speckle.Core.Serialisation.TypeCache;

public interface ITypeCache
{
  void EnsureCacheIsBuilt();

  (Version version, CachedTypeInfo cachedTypeInfo)  GetMatchedTypeOrLater(string speckleType, Version versionToMatch);
  
  Version LoadedSchemaVersion { get; }
}
