namespace Speckle.Core.Serialisation.TypeCache;

public interface ITypeCache
{
  void EnsureCacheIsBuilt();

  CachedTypeInfo GetMatchedTypeOrLater(string speckleType, Version versionToMatch);
  
  Version LoadedSchemaVersion { get; }
}
