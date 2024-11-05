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
  private readonly ConcurrentDictionary<string, ObjectReference> _objectReferences = new();

  private long _total;
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
    await Traverse(root, true, cancellationToken).ConfigureAwait(false);
    await channelTask.ConfigureAwait(false);
    return (root.id, _objectReferences);
  }

  private async Task<List<Dictionary<string, int>>> Traverse(Base obj, bool isEnd, CancellationToken cancellationToken)
  {
    var tasks = new List<Task<List<Dictionary<string, int>>>>();
    foreach (var child in speckleBaseChildFinder.GetChildren(obj))
    {
      Interlocked.Increment(ref _total);
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
    var closures = tasks
      .Select(t => t.Result)
      .Aggregate(
        new List<Dictionary<string, int>>(),
        (a, s) =>
        {
          a.AddRange(s);
          return a;
        }
      )
      .ToList();

    var item = Serialise(obj, closures);
    Interlocked.Increment(ref _serialized);
    progress?.Report(new(ProgressEvent.FromCacheOrSerialized, _serialized, _total));
    if (item?.NeedsStorage ?? false)
    {
      await Save(item.Value, cancellationToken).ConfigureAwait(false);
    }
    if (isEnd)
    {
      Done(obj.id);
    }
    return closures;
  }

  //leave this sync
  private BaseItem? Serialise(Base obj, List<Dictionary<string, int>> childClosures)
  {
    if (obj.id != null && _jsonCache.ContainsKey(obj.id))
    {
      return null;
    }

    string? json = null;
    if (!_options.SkipCache && obj.id != null)
    {
      json = sqliteSendCacheManager.GetObject(obj.id);
    }
    if (json == null)
    {
      var id = obj.id;
      if (id is null || !_jsonCache.TryGetValue(id, out json))
      {
        SpeckleObjectSerializer2 serializer2 = new(speckleBasePropertyGatherer, childClosures);
        json = serializer2.Serialize(obj);
        obj.id.NotNull();
        foreach (var kvp in serializer2.ObjectReferences)
        {
          _objectReferences.TryAdd(kvp.Key, kvp.Value);
        }

        _jsonCache.TryAdd(obj.id, json);
        if (id is not null && id != obj.id)
        {
          //in case the ids changes which is due to id hash algorithm changing
          _jsonCache.TryAdd(id, json);
        }
      }
      return new BaseItem(obj.id.NotNull(), json, true);
    }
    return new BaseItem(obj.id.NotNull(), json.NotNull(), false);
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
      progress?.Report(new(ProgressEvent.CachedToLocal, _cached, null));
    }
  }
}
