using System.Collections.Concurrent;
using System.Threading.Channels;
using Open.ChannelExtensions;
using Speckle.Sdk.Models;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Serialisation.V2.Send;

public class SerializeProcess(
  IProgress<ProgressArgs>? progress,
  ISQLiteCacheManager sqliteCacheManager,
  IServerObjectManager serverObjectManager,
  ISpeckleBaseChildFinder speckleBaseChildFinder,
  ISpeckleBasePropertyGatherer speckleBasePropertyGatherer
)
{
  public ConcurrentDictionary<string, string> JsonCache { get; } = new();
  private readonly Channel<(string, string)> _checkCacheChannel = Channel.CreateUnbounded<(string, string)>();
  private long _total;
  private long _checked;
  private long _serialized;

  public async Task Serialize(string streamId, Base root, CancellationToken cancellationToken)
  {
    var channelTask = _checkCacheChannel
      .Reader.Pipe(Environment.ProcessorCount, x => CheckCache(root.id, x), -1, false, cancellationToken)
      .Filter(x => x != null)
      .Batch(1000)
      .WithTimeout(TimeSpan.FromSeconds(2))
      .PipeAsync(4, x => SendToServer(streamId, x, cancellationToken), -1, false, cancellationToken)
      .Join()
      .ReadAllConcurrently(Environment.ProcessorCount, x => SaveToCache(root.id, x), cancellationToken);
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

      //await Traverse(child, cancellationToken).ConfigureAwait(false);
      // tmp is necessary because of the way closures close over loop variables

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
    }

    if (tasks.Count > 0)
    {
      await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    var json = Serialise(obj);
    if (json is not null)
    {
      await _checkCacheChannel.Writer.WriteAsync((obj.id, json), cancellationToken).ConfigureAwait(false);
    }
  }

  //leave this sync
  private string? Serialise(Base obj)
  {
    if (JsonCache.ContainsKey(obj.id))
    {
      return null;
    }

    var json = sqliteCacheManager.GetObject(obj.id);
    Interlocked.Increment(ref _total);
    if (json == null)
    {
      if (JsonCache.TryGetValue(obj.id, out json))
      {
        Interlocked.Increment(ref _serialized);
        SpeckleObjectSerializer2 serializer2 = new(speckleBasePropertyGatherer, JsonCache, progress);
        Console.WriteLine("Serialized " + JsonCache.Count + " " + _total + " " + _serialized);
        json = serializer2.Serialize(obj);
        JsonCache.TryAdd(obj.id, json);
        progress?.Report(new(ProgressEvent.SerializeObject, JsonCache.Count, _total));
      }
    }
    return json;
  }

  private (string, string)? CheckCache(string rootId, (string, string) item)
  {
    Interlocked.Increment(ref _checked);
    progress?.Report(new(ProgressEvent.CacheCheck, _checked, null));
    if (!sqliteCacheManager.HasObject(item.Item1))
    {
      return item;
    }
    if (rootId == item.Item1)
    {
      _checkCacheChannel.Writer.Complete();
    }
    return null;
  }

  private async ValueTask<List<(string, string)>> SendToServer(
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
    await serverObjectManager
      .UploadObjects(streamId, batchToSend, true, progress, cancellationToken)
      .ConfigureAwait(false);
    return batchToSend;
  }

  private void SaveToCache(string rootId, (string, string) item)
  {
    sqliteCacheManager.SaveObjectSync(item.Item1, item.Item2);
    if (rootId == item.Item1)
    {
      _checkCacheChannel.Writer.Complete();
    }
  }
}
