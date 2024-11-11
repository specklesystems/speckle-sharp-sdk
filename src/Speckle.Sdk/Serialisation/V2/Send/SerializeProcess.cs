using System.Collections.Concurrent;
using Speckle.InterfaceGenerator;
using Speckle.Sdk.Common;
using Speckle.Sdk.Dependencies.Serialization;
using Speckle.Sdk.Models;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Serialisation.V2.Send;

public record SerializeProcessOptions(bool SkipCacheRead, bool SkipCacheWrite, bool SkipServer);

[GenerateAutoInterface]
public class SerializeProcess(
  IProgress<ProgressArgs>? progress,
  ISQLiteSendCacheManager sqliteSendCacheManager,
  IServerObjectManager serverObjectManager,
  IBaseChildFinder baseChildFinder,
  IObjectSerializerFactory objectSerializerFactory
) : ChannelSaver, ISerializeProcess
{
  private readonly ConcurrentDictionary<string, string> _jsonCache = new();
  private readonly ConcurrentDictionary<Base, (string, Dictionary<string, int>)> _baseCache = new();
  private readonly ConcurrentDictionary<string, ObjectReference> _objectReferences = new();

  private long _totalFound;
  private long _totalToUpload;
  private long _uploaded;
  private long _cached;
  private long _serialized;

  private SerializeProcessOptions _options = new(false, false, false);

  public async Task<(string rootObjId, IReadOnlyDictionary<string, ObjectReference> convertedReferences)> Serialize(
    Base root,
    CancellationToken cancellationToken,
    SerializeProcessOptions? options = null
  )
  {
    _options = options ?? _options;
    var channelTask = Start(cancellationToken);
    await Traverse(root, true, cancellationToken).ConfigureAwait(false);
    await channelTask.ConfigureAwait(false);
    return (root.id, _objectReferences);
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
    if (obj.id != null && _jsonCache.ContainsKey(obj.id))
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
    var id = obj.id;
    if (id is null || !_jsonCache.TryGetValue(id, out var json))
    {
      var serializer2 = objectSerializerFactory.Create(_baseCache, cancellationToken);
      var items = serializer2.Serialize(obj).ToList();
      foreach (var kvp in serializer2.ObjectReferences)
      {
        _objectReferences.TryAdd(kvp.Key, kvp.Value);
      }

      var (_, j) = items.First();
      json = j;
      _jsonCache.TryAdd(obj.id.NotNull(), j);
      yield return CheckCache(obj.id.NotNull(), j);
      if (id is not null && id != obj.id)
      {
        //in case the ids changes which is due to id hash algorithm changing
        _jsonCache.TryAdd(id, json);
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
      yield return new BaseItem(obj.id.NotNull(), json, true);
    }
  }

  private BaseItem CheckCache(string id, string json)
  {
    if (!_options.SkipCacheRead)
    {
      var cachedJson = sqliteSendCacheManager.GetObject(id);
      if (cachedJson != null)
      {
        return new BaseItem(id, cachedJson, false);
      }
    }
    return new BaseItem(id, json, true);
  }

  public override async Task<List<BaseItem>> SendToServer(List<BaseItem> batch, CancellationToken cancellationToken)
  {
    if (batch.Count == 0)
    {
      progress?.Report(new(ProgressEvent.UploadedObjects, _uploaded, _totalToUpload));
      return batch;
    }

    if (!_options.SkipServer)
    {
      await serverObjectManager.UploadObjects(batch, true, progress, cancellationToken).ConfigureAwait(false);
      Interlocked.Exchange(ref _uploaded, _uploaded + batch.Count);
      progress?.Report(new(ProgressEvent.UploadedObjects, _uploaded, _totalToUpload));
    }
    return batch;
  }

  public override void SaveToCache(List<BaseItem> items)
  {
    if (!_options.SkipCacheWrite)
    {
      if (items.Count == 0)
      {
        progress?.Report(new(ProgressEvent.CachedToLocal, _cached, null));
        return;
      }
      sqliteSendCacheManager.SaveObjects(items);
      Interlocked.Exchange(ref _cached, _cached + items.Count);
      progress?.Report(new(ProgressEvent.CachedToLocal, _cached, null));
    }
  }
}
