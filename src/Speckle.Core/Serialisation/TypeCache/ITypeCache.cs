namespace Speckle.Core.Serialisation.TypeCache;

public interface ITypeCache
{
  void EnsureCacheIsBuilt();
  CachedTypeInfo GetType(string speckelType);
}
