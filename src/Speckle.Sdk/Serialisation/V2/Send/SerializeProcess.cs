using System.Collections.Concurrent;
using Speckle.Sdk.Common;
using Speckle.Sdk.Dependencies.Serialization;
using Speckle.Sdk.Models;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Serialisation.V2.Send;

public record SerializeProcessOptions(bool SkipCache, bool SkipServer);

public class SerializeProcess(
  IProgress<ProgressArgs>? progress,
  ISQLiteSendCacheManager sqliteSendCacheManager,
  IServerObjectManager serverObjectManager,
  ISpeckleBaseChildFinder speckleBaseChildFinder,
  ISpeckleBasePropertyGatherer speckleBasePropertyGatherer
) : ChannelSaver
{
  private readonly ConcurrentDictionary<string, string> _jsonCache = new();
  private readonly ConcurrentDictionary<string, Task> _activeTasks = new();
  private readonly ConcurrentDictionary<string, ObjectReference> _objectReferences = new();

  private long _total;
  private long _checked;
  private long _cached;
  private long _serialized;

  private SerializeProcessOptions _options = new(false, false);

  public async Task<(string rootObjId, IReadOnlyDictionary<string, ObjectReference> convertedReferences)> Serialize(
    string streamId,
    Base root,
    CancellationToken cancellationToken,
    SerializeProcessOptions? options = null
  )
  {
    _options = options ?? _options;
    var channelTask = Start(streamId, cancellationToken);
    await Traverse(root.id, root, true, cancellationToken).ConfigureAwait(false);
    await channelTask.ConfigureAwait(false);
    return (root.id, _objectReferences);
  }

  private async Task Traverse(string? id, Base obj, bool isEnd, CancellationToken cancellationToken)
  {
    if (id != null && _jsonCache.ContainsKey(id))
    {
      return;
    }
    var tasks = new List<Task>();
    foreach (var child in speckleBaseChildFinder.GetChildren(obj))
    {
      if (child.id != null && _jsonCache.ContainsKey(child.id))
      {
        continue;
      }

      // tmp is necessary because of the way closures close over loop variables
      if (child.id != null && _activeTasks.TryGetValue(child.id, out var task))
      {
        tasks.Add(task);
      }
      else
      {
        var tmp = child;
        var t = Task
          .Factory.StartNew(
            () => Traverse(tmp.id, tmp, false, cancellationToken),
            cancellationToken,
            TaskCreationOptions.AttachedToParent,
            TaskScheduler.Default
          )
          .Unwrap();
        tasks.Add(t);
        if (child.id != null)
        {
          _activeTasks.TryAdd(child.id, t);
        }
      }
    }

    if (tasks.Count > 0)
    {
      await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    var item = Serialise(obj.id, obj, isEnd);
    if (item is not null)
    {
      await Save(item.Value, cancellationToken).ConfigureAwait(false);
    }
  }

  //leave this sync
  private BaseItem? Serialise(string? id, Base obj, bool isEnd)
  {
    if (id != null && _jsonCache.ContainsKey(id))
    {
      return null;
    }

    string? json = null;
    if (!_options.SkipCache && id != null)
    {
      json = sqliteSendCacheManager.GetObject(id);
    }
    Interlocked.Increment(ref _total);
    if (json == null)
    {
      if (id is null || !_jsonCache.TryGetValue(id, out json))
      {
        SpeckleObjectSerializer2 serializer2 = new(speckleBasePropertyGatherer, _jsonCache);
        json = serializer2.Serialize(obj);
        obj.id.NotNull();
        foreach (var kvp in serializer2.ObjectReferences)
        {
          _objectReferences.TryAdd(kvp.Key, kvp.Value);
        }
        _jsonCache.TryAdd(obj.id, json);
        if (id is not null)
        {
          _activeTasks.TryRemove(id, out _);
          if (id != obj.id) //in case the ids changes which is due to id hash algorithm changing
          {
            _jsonCache.TryAdd(obj.id, json);
            _activeTasks.TryRemove(obj.id, out _);
          }
        }
        Interlocked.Increment(ref _serialized);
        progress?.Report(new(ProgressEvent.SerializeObject, _serialized, _total));
      }
    }
    else if (id != null)
    {
      _jsonCache.TryAdd(id, json);
      _activeTasks.TryRemove(id, out _);
    }
    return new BaseItem(obj.id.NotNull(), json, isEnd);
  }

  //return null when it's cached
  public override List<BaseItem> CheckCache(List<BaseItem> items)
  {
    List<BaseItem> result;
    progress?.Report(new(ProgressEvent.CacheCheck, _checked, _total));
    if (!_options.SkipCache)
    {
      result = items.Except(sqliteSendCacheManager.HasObjects(items)).ToList();
    }
    else
    {
      result = new();
    }
    Interlocked.Exchange(ref _checked, _checked + items.Count);

    if (items.Any(x => x.IsEnd))
    {
      Done();
    }
    return result;
  }

  public override async Task<List<BaseItem>> SendToServer(
    string streamId,
    List<BaseItem> batch,
    CancellationToken cancellationToken
  )
  {
    if (batch.Count == 0)
    {
      return batch;
    }

    if (!_options.SkipServer)
    {
      await serverObjectManager.UploadObjects(streamId, batch, true, progress, cancellationToken).ConfigureAwait(false);
    }
    return batch;
  }

  public override void SaveToCache(List<BaseItem> items)
  {
    if (!_options.SkipCache)
    {
      sqliteSendCacheManager.SaveObjects(items);
      Interlocked.Exchange(ref _cached, _cached + items.Count);
      progress?.Report(new(ProgressEvent.Cached, _cached, null));
    }

    if (items.Any(x => x.IsEnd))
    {
      Done();
    }
  }
}
