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
  public ConcurrentDictionary<string, string> JsonCache { get; } = new();
  private long _total;
  private long _checked;
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
    if (JsonCache.ContainsKey(obj.id))
    {
      return;
    }
    var tasks = new List<Task>();
    var children = speckleBaseChildFinder.GetChildren(obj).ToList();
    foreach (var child in children)
    {
      if (JsonCache.ContainsKey(child.id))
      {
        continue;
      }

      await Traverse(child, cancellationToken).ConfigureAwait(false);
      // tmp is necessary because of the way closures close over loop variables
      /*
            var tmp = child;
            var t = Task
              .Factory.StartNew(
                () => Traverse(tmp, cancellationToken),
                cancellationToken,
                TaskCreationOptions.AttachedToParent,
                TaskScheduler.Default
              )
              .Unwrap();
            tasks.Add(t);*/
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
    if (JsonCache.ContainsKey(obj.id))
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
      if (!JsonCache.TryGetValue(obj.id, out json))
      {
        Interlocked.Increment(ref _serialized);
        SpeckleObjectSerializer2 serializer2 = new(speckleBasePropertyGatherer, JsonCache, progress);
        Console.WriteLine("Serialized " + JsonCache.Count + " " + _total + " " + _serialized);
        json = serializer2.Serialize(obj);
        JsonCache.TryAdd(obj.id, json);
        progress?.Report(new(ProgressEvent.SerializeObject, JsonCache.Count, _total));
      }
    }
    else
    {
      JsonCache.TryAdd(obj.id, json);
    }
    return json;
  }

  //return null when it's cached
  public override (string, string)? CheckCache(string rootId, (string, string) item)
  {
    Interlocked.Increment(ref _checked);
    progress?.Report(new(ProgressEvent.CacheCheck, _checked, null));
    if (!_options.SkipCache)
    {
      if (!sqliteCacheManager.HasObject(item.Item1))
      {
        return item;
      }
    }

    if (rootId == item.Item1)
    {
      Done();
    }
    return null;
  }

  public override async Task<List<(string, string)>> SendToServer(
    string streamId,
    IReadOnlyList<(string, string)?> batch,
    CancellationToken cancellationToken
  )
  {
    var batchToSend = batch.Where(x => x != null).Cast<(string, string)>().ToList();
    if (batchToSend.Count == 0)
    {
      return batchToSend;
    }

    if (!_options.SkipServer)
    {
      await serverObjectManager
        .UploadObjects(streamId, batchToSend, true, progress, cancellationToken)
        .ConfigureAwait(false);
    }
    return batchToSend;
  }

  public override void SaveToCache(string rootId, (string, string) item)
  {
    if (!_options.SkipCache)
    {
      sqliteCacheManager.SaveObjectSync(item.Item1, item.Item2);
    }

    if (rootId == item.Item1)
    {
      Done();
    }
  }
}
