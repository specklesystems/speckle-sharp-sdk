using System.Collections.Concurrent;
using Speckle.InterfaceGenerator;
using Speckle.Sdk.Common;
using Speckle.Sdk.Dependencies;
using Speckle.Sdk.Models;
using Speckle.Sdk.SQLite;
using Closures = System.Collections.Generic.Dictionary<Speckle.Sdk.Serialisation.Id, int>;

namespace Speckle.Sdk.Serialisation.V2.Send;

[GenerateAutoInterface]
public class BaseSerializer(
  ISqLiteJsonCacheManager sqLiteJsonCacheManager,
  IObjectSerializerFactory objectSerializerFactory
) : IBaseSerializer
{
  private readonly Pool<List<(Id, Json, Closures)>> _pool = Pools.CreateListPool<(Id, Json, Closures)>();

  private readonly ConcurrentDictionary<Id, ObjectReference> _objectReferences = new();

  public IReadOnlyDictionary<Id, ObjectReference> ObjectReferences => _objectReferences;

  //leave this sync
  public IEnumerable<BaseItem> Serialise(Base obj, bool skipCacheRead, CancellationToken cancellationToken)
  {
    if (!skipCacheRead && obj.id != null)
    {
      var cachedJson = sqLiteJsonCacheManager.GetObject(obj.id);
      if (cachedJson != null)
      {
        yield return new BaseItem(new(obj.id.NotNull()), new(cachedJson), false, null);
        yield break;
      }
    }

    using var serializer2 = objectSerializerFactory.Create(cancellationToken);
    var items = _pool.Get();
    try
    {
      items.AddRange(serializer2.Serialize(obj));
      foreach (var kvp in serializer2.ObjectReferences)
      {
        _objectReferences.TryAdd(kvp.Key, kvp.Value);
      }

      var (id, json, closures) = items.First();
      yield return CheckCache(id, json, closures, skipCacheRead);
      foreach (var (cid, cJson, cClosures) in items.Skip(1))
      {
        yield return CheckCache(cid, cJson, cClosures, skipCacheRead);
      }
    }
    finally
    {
      _pool.Return(items);
    }
  }

  private BaseItem CheckCache(Id id, Json json, Dictionary<Id, int> closures, bool skipCacheRead)
  {
    if (!skipCacheRead)
    {
      var cachedJson = sqLiteJsonCacheManager.GetObject(id.Value);
      if (cachedJson != null)
      {
        return new BaseItem(id, new(cachedJson), false, null);
      }
    }
    return new BaseItem(id, json, true, closures);
  }
}
