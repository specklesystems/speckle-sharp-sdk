using System.Collections.Concurrent;
using Speckle.InterfaceGenerator;
using Speckle.Sdk.Logging;
using Speckle.Sdk.Serialisation.Utilities;

namespace Speckle.Sdk.SQLite;

public partial interface ISqLiteJsonCacheManagerFactory : IDisposable;

[GenerateAutoInterface]
public sealed class SqLiteJsonCacheManagerFactory : ISqLiteJsonCacheManagerFactory
{
  public const int INITIAL_CONCURRENCY = 4;

  private readonly ConcurrentDictionary<string, ISqliteJsonCachePool> _pools = new();

  [AutoInterfaceIgnore]
  public void Dispose()
  {
    foreach (var pool in _pools)
    {
      pool.Value.Dispose();
    }

    _pools.Clear();
  }

  private ISqliteJsonCachePool Create(string path, int concurrency) => new SqliteJsonCachePool(path, concurrency);

  public ISqLiteJsonCacheManager CreateForUser(string scope) =>
    new SqLiteJsonCacheManager(
#pragma warning disable CA2000
      //this is fine because we told SqLiteJsonCacheManager to dispose this
      Create(Path.Combine(SpecklePathProvider.UserApplicationDataPath(), "Speckle", $"{scope}.db"), 1),
#pragma warning restore CA2000
      true
    );

  public ISqLiteJsonCacheManager CreateFromStream(string streamId)
  {
    if (!_pools.TryGetValue(streamId, out var pool))
    {
      pool = Create(SqlitePaths.GetDBPath(streamId), INITIAL_CONCURRENCY);
      _pools.TryAdd(streamId, pool);
    }
    //never dispose pools for streams
    return new SqLiteJsonCacheManager(pool, false);
  }
}
