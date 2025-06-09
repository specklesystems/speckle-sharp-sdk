using System.Collections.Concurrent;
using Speckle.Sdk.SQLite;

namespace Speckle.Sdk.Serialisation.V2;

#pragma warning disable CA1063
public class MemoryJsonCacheManager(ConcurrentDictionary<Id, Json> jsonCache) : ISqLiteJsonCacheManager
#pragma warning restore CA1063
{
#pragma warning disable CA1065
  public ISqliteJsonCachePool Pool => throw new NotImplementedException();
#pragma warning restore CA1065
  public IReadOnlyCollection<(string Id, string Json)> GetAllObjects() =>
    jsonCache.Select(x => (x.Key.Value, x.Value.Value)).ToList();

  public void DeleteObject(string id) => jsonCache.TryRemove(new Id(id), out _);

  public string? GetObject(string id) => jsonCache.TryGetValue(new Id(id), out var json) ? json.Value : null;

  public void SaveObject(string id, string json) => jsonCache.TryAdd(new Id(id), new Json(json));

  public void UpdateObject(string id, string json) => jsonCache[new Id(id)] = new Json(json);

  public virtual void SaveObjects(IEnumerable<(string id, string json)> items)
  {
    foreach (var (id, json) in items)
    {
      SaveObject(id, json);
    }
  }

  public bool HasObject(string objectId) => jsonCache.ContainsKey(new Id(objectId));

#pragma warning disable CA1063
#pragma warning disable CA1816
  public void Dispose()
#pragma warning restore CA1816
#pragma warning restore CA1063
  { }
}
