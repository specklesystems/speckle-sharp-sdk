using System.Collections.Concurrent;
using Speckle.Sdk.Models;

namespace Speckle.Sdk.Serialisation.Receive;

public sealed class CachingStage(
  SqliteManagerOptions options,
  ConcurrentDictionary<string, Base> memoryCache,
  Func<string, CancellationToken, ValueTask> notCached,
  Func<Downloaded, CancellationToken, ValueTask> sqliteCached,
  Action<Deserialized> done
) : IDisposable
{
  private readonly SqliteManager _sqLiteManager = new(options);

  public async ValueTask Execute(IReadOnlyList<string> ids, CancellationToken cancellationToken)
  {
    var notMemoryCached = new List<string>(ids.Count);
    foreach (var id in ids)
    {
      if (memoryCache.TryGetValue(id, out var memory))
      {
        done(new Deserialized(id, memory));
      }
      else
      {
        notMemoryCached.Add(id);
      }
    }
    foreach (var (id, json) in _sqLiteManager.GetObjects(notMemoryCached, cancellationToken))
    {
      if (json is null)
      {
        await notCached(id, cancellationToken).ConfigureAwait(false);
      }
      else
      {
        await sqliteCached(new(id, json), cancellationToken).ConfigureAwait(false);
      }
    }
  }

  public void Dispose() => _sqLiteManager.Dispose();
}
