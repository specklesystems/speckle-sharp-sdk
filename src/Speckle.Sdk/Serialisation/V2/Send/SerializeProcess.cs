using System.Collections.Concurrent;
using Speckle.InterfaceGenerator;
using Speckle.Sdk.Common;
using Speckle.Sdk.Dependencies;
using Speckle.Sdk.Dependencies.Serialization;
using Speckle.Sdk.Models;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Serialisation.V2.Send;

public record SerializeProcessOptions(bool SkipCacheRead, bool SkipCacheWrite, bool SkipServer);

public readonly record struct SerializeProcessResults(
  string RootId,
  IReadOnlyDictionary<Id, ObjectReference> ConvertedReferences
);

[GenerateAutoInterface]
public class SerializeProcess(
  IProgress<ProgressArgs>? progress,
  ISQLiteSendCacheManager sqliteSendCacheManager,
  IServerObjectManager serverObjectManager,
  IBaseChildFinder baseChildFinder,
  IObjectSerializerFactory objectSerializerFactory
) : ChannelSaver, ISerializeProcess
{
  private readonly ConcurrentDictionary<Id, Json> _jsonCache = new();
  private readonly ConcurrentDictionary<Base, CacheInfo> _baseCache = new();
  private readonly ConcurrentDictionary<Id, ObjectReference> _objectReferences = new();

  private long _totalFound;
  private long _totalToUpload;
  private long _uploaded;
  private long _cached;
  private long _serialized;

  private SerializeProcessOptions _options = new(false, false, false);

  public async Task<SerializeProcessResults> Serialize(
    Base root,
    CancellationToken cancellationToken,
    SerializeProcessOptions? options = null
  )
  {
    _options = options ?? _options;
    var channelTask = Start(cancellationToken);
    await Traverse(root, true, cancellationToken).ConfigureAwait(false);
    await channelTask.ConfigureAwait(false);
    return new(root.id, _objectReferences.Freeze());
  }

  private async Task Traverse(Base obj, bool isEnd, CancellationToken cancellationToken)
  {
    var tasks = new List<Task>();
    foreach (var child in baseChildFinder.GetChildren(obj))
    {
      Interlocked.Increment(ref _totalFound);
      progress?.Report(new(ProgressEvent.FindingChildren, _totalFound, null));
      // tmp is necessary because of the way closures close over loop variables
      var tmp = child;
      var t = Task
        .Factory.StartNew(
          () => Traverse(tmp, false, cancellationToken),
          cancellationToken,
          TaskCreationOptions.AttachedToParent,
          TaskScheduler.Default
        )
        .Unwrap();
      tasks.Add(t);
    }

    if (tasks.Count > 0)
    {
      await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    var items = Serialise(obj, cancellationToken);
    foreach (var item in items)
    {
      Interlocked.Increment(ref _serialized);
      progress?.Report(new(ProgressEvent.FromCacheOrSerialized, _serialized, _totalFound));
      if (item.NeedsStorage)
      {
        Interlocked.Increment(ref _totalToUpload);
        await Save(item, cancellationToken).ConfigureAwait(false);
      }
    }

    if (isEnd)
    {
      await Done().ConfigureAwait(false);
    }
  }

  //leave this sync
  private IEnumerable<BaseItem> Serialise(Base obj, CancellationToken cancellationToken)
  {
    Id? id = obj.id != null ? new Id(obj.id) : null;
    if (id != null && _jsonCache.ContainsKey(id.Value))
    {
      yield break;
    }

    if (!_options.SkipCacheRead && obj.id != null)
    {
      var cachedJson = sqliteSendCacheManager.GetObject(obj.id);
      if (cachedJson != null)
      {
        yield return new BaseItem(obj.id.NotNull(), cachedJson, false);
        yield break;
      }
    }
    if (id is null || !_jsonCache.TryGetValue(id.Value, out var json))
    {
      var serializer2 = objectSerializerFactory.Create(_baseCache, cancellationToken);
      var items = serializer2.Serialize(obj).ToList();
      foreach (var kvp in serializer2.ObjectReferences)
      {
        _objectReferences.TryAdd(kvp.Key, kvp.Value);
      }

      var newId = new Id(obj.id.NotNull());
      var (_, j) = items.First();
      json = j;
      _jsonCache.TryAdd(newId, j);
      yield return CheckCache(newId, j);
      if (id is not null && id != newId)
      {
        //in case the ids changes which is due to id hash algorithm changing
        _jsonCache.TryAdd(id.Value, json);
      }
      foreach (var (cid, cJson) in items.Skip(1))
      {
        if (_jsonCache.TryAdd(cid, cJson))
        {
          Interlocked.Increment(ref _totalFound);
          yield return CheckCache(cid, cJson);
        }
      }
    }
    else
    {
      yield return new BaseItem(id.NotNull().Value, json.Value, true);
    }
  }

  private BaseItem CheckCache(Id id, Json json)
  {
    if (!_options.SkipCacheRead)
    {
      var cachedJson = sqliteSendCacheManager.GetObject(id.Value);
      if (cachedJson != null)
      {
        return new BaseItem(id.Value, cachedJson, false);
      }
    }
    return new BaseItem(id.Value, json.Value, true);
  }

  public override async Task<List<BaseItem>> SendToServer(List<BaseItem> batch, CancellationToken cancellationToken)
  {
    if (!_options.SkipServer && batch.Count != 0)
    {
      await serverObjectManager.UploadObjects(batch, true, progress, cancellationToken).ConfigureAwait(false);
      Interlocked.Exchange(ref _uploaded, _uploaded + batch.Count);
      progress?.Report(new(ProgressEvent.UploadedObjects, _uploaded, _totalToUpload));
    }
    return batch;
  }

  public override void SaveToCache(List<BaseItem> batch)
  {
    if (!_options.SkipCacheWrite && batch.Count != 0)
    {
      sqliteSendCacheManager.SaveObjects(batch);
      Interlocked.Exchange(ref _cached, _cached + batch.Count);
      progress?.Report(new(ProgressEvent.CachedToLocal, _cached, null));
    }
  }
}
