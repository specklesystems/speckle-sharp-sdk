using System.Collections.Concurrent;
using Speckle.Sdk.Dependencies.Serialization;
using Speckle.Sdk.Models;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Serialisation.V2.Send;

public record SerializeProcessOptions(bool SkipCache, bool SkipServer);

public class SerializeProcess(
  IProgress<ProgressArgs>? progress,
  ISQLiteCacheManager sqliteCacheManager,
  IServerObjectManager serverObjectManager,
  ISpeckleBaseChildFinder speckleBaseChildFinder,
  ISpeckleBasePropertyGatherer speckleBasePropertyGatherer
) : ChannelSaver
{
  private readonly ConcurrentDictionary<string, string> _jsonCache = new();
  private readonly ConcurrentDictionary<string, Task> _activeTasks = new();
  private long _total;
  private long _checked;
  private long _cached;
  private long _serialized;

  private SerializeProcessOptions _options = new(false, false);

  public async Task Serialize(
    string streamId,
    Base root,
    CancellationToken cancellationToken,
    SerializeProcessOptions? options = null
  )
  {
    _options = options ?? _options;
    var channelTask = Start(streamId, root.id, cancellationToken);
    await Traverse(root, cancellationToken).ConfigureAwait(false);
    await channelTask.ConfigureAwait(false);
  }

  private async Task Traverse(Base obj, CancellationToken cancellationToken)
  {
    if (_jsonCache.ContainsKey(obj.id))
    {
      return;
    }
    var tasks = new List<Task>();
    foreach (var child in speckleBaseChildFinder.GetChildren(obj))
    {
      if (_jsonCache.ContainsKey(child.id))
      {
        continue;
      }

      // tmp is necessary because of the way closures close over loop variables
      if (_activeTasks.TryGetValue(child.id, out var task))
      {
        tasks.Add(task);
      }
      else
      {
        var tmp = child;
        var t = Task
          .Factory.StartNew(
            () => Traverse(tmp, cancellationToken),
            cancellationToken,
            TaskCreationOptions.AttachedToParent,
            TaskScheduler.Default
          )
          .Unwrap();
        tasks.Add(t);
        _activeTasks.TryAdd(child.id, t);
      }
    }

    if (tasks.Count > 0)
    {
      await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    var json = Serialise(obj);
    if (json is not null)
    {
      await Save(obj.id, json, cancellationToken).ConfigureAwait(false);
    }
  }

  //leave this sync
  private string? Serialise(Base obj)
  {
    if (_jsonCache.ContainsKey(obj.id))
    {
      return null;
    }

    string? json = null;
    if (!_options.SkipCache)
    {
      json = sqliteCacheManager.GetObject(obj.id);
    }
    Interlocked.Increment(ref _total);
    if (json == null)
    {
      string id = obj.id;
      if (!_jsonCache.TryGetValue(id, out json))
      {
        SpeckleObjectSerializer2 serializer2 = new(speckleBasePropertyGatherer, _jsonCache, progress);
        json = serializer2.Serialize(obj);
        _jsonCache.TryAdd(id, json);
        _activeTasks.TryRemove(id, out _);
        if (id != obj.id) //in case the ids changes which is due to id hash algorithm changing
        {
          _jsonCache.TryAdd(obj.id, json);
          _activeTasks.TryRemove(obj.id, out _);
        }
        Interlocked.Increment(ref _serialized);
        progress?.Report(new(ProgressEvent.SerializeObject, _serialized, _total));
      }
    }
    else
    {
      _jsonCache.TryAdd(obj.id, json);
      _activeTasks.TryRemove(obj.id, out _);
    }
    return json;
  }

  //return null when it's cached
  public override BaseItem? CheckCache(string rootId, BaseItem item)
  {
    bool result = true;
    progress?.Report(new(ProgressEvent.CacheCheck, _checked, _total));
    if (!_options.SkipCache)
    {
      result = sqliteCacheManager.HasObject(item.Id);
    }
    Interlocked.Increment(ref _checked);

    if (item.Id == rootId)
    {
      Done();
    }

    if (result)
    {
      return null;
    }
    return item;
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

  public override void SaveToCache(string rootId, List<BaseItem> items)
  {
    if (!_options.SkipCache)
    {
      sqliteCacheManager.SaveObjects(items);
      Interlocked.Exchange(ref _cached, _cached + items.Count);
      progress?.Report(new(ProgressEvent.Cached, _cached, null));
    }

    if (items.Any(x => x.Id == rootId))
    {
      Done();
    }
  }
}
