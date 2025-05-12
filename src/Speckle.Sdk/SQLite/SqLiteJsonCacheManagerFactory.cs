using System.Collections.Concurrent;
using Speckle.InterfaceGenerator;
using Speckle.Sdk.Logging;
using Speckle.Sdk.Serialisation.Utilities;

namespace Speckle.Sdk.SQLite;

[GenerateAutoInterface]
public class SqLiteJsonCacheManagerFactory : ISqLiteJsonCacheManagerFactory
{
  public const int INITIAL_CONCURRENCY = 4;
  
  private readonly ConcurrentDictionary<string, ISqLiteJsonCacheManager> _cachedManagers = new();

  private ISqLiteJsonCacheManager Create(string path, int concurrency) => new SqLiteJsonCacheManager(path, concurrency);

  public ISqLiteJsonCacheManager CreateForUser(string scope) =>
    Create(Path.Combine(SpecklePathProvider.UserApplicationDataPath(), "Speckle", $"{scope}.db"), 1);

  public ISqLiteJsonCacheManager CreateFromStream(string streamId)
  {
    if (!_cachedManagers.TryGetValue(streamId, out var manager))
    {
      manager = Create(SqlitePaths.GetDBPath(streamId), INITIAL_CONCURRENCY);
      _cachedManagers.TryAdd(streamId, manager);
    }
    return manager;
  }
}
