using System.Collections.Concurrent;
using System.Threading.Channels;
using Open.ChannelExtensions;
using Speckle.Sdk.Models;
using Speckle.Sdk.Transports;
using Speckle.Sdk.Transports.ServerUtils;

namespace Speckle.Sdk.Serialisation.Receive;

public record ReceiveProcessSettings(
  int MaxDownloadThreads = 4,
  int MaxDeserializeThreads = 4,
  int MaxObjectRequestSize = ServerApi.BATCH_SIZE_GET_OBJECTS - 1,
  int BatchWaitMilliseconds = 500
);

public sealed class ReceiveProcess : IDisposable
{
  private readonly ReceiveProcessSettings _settings = new();
  private readonly ConcurrentDictionary<string, Base> _idToBaseCache = new(StringComparer.Ordinal);

  private long _received;

  public ReceiveProcess(
    Uri baseUri,
    string streamId,
    string? authorizationToken,
    ReceiveProcessSettings? settings = null
  )
  {
    if (settings is not null)
    {
      _settings = settings;
    }
    SourceChannel = Channel.CreateUnbounded<string>();
    ReceiveChannel = Channel.CreateUnbounded<Base>();
    CachingStage = new(_idToBaseCache);
    ServerApiStage = new ServerApiStage(baseUri, streamId, authorizationToken, args =>
    {
      _bytes = args.Count ?? 0;
      InvokeProgress();
    });
    DeserializeStage = new(_idToBaseCache, ReceiveAsync);
  }

  private Channel<string> SourceChannel { get; }
  private Channel<Base> ReceiveChannel { get; }

  private long _bytes;
  public Action<ProgressArgs[]>? Progress { get; set; }

  public void InvokeProgress() =>
    Progress?.Invoke(
      [
        new ProgressArgs(ProgressEvent.DownloadBytes, _bytes, null),
        new ProgressArgs(ProgressEvent.DeserializeObject, DeserializeStage.Deserialized, null),
        new ProgressArgs(ProgressEvent.DownloadObject, _received, null),
      ]
    );

  public async ValueTask ReceiveAsync(string id) => await SourceChannel.Writer.WriteAsync(id).ConfigureAwait(false);

  
  public IAsyncEnumerable<Base> GetBases(CancellationToken cancellationToken)
  {
    var enumerable = ReceiveChannel.Reader
      .AsAsyncEnumerable(cancellationToken: cancellationToken)
      .Select(x =>
      {
        _received++;
        return x;
      });
    return enumerable;
  }

  public async ValueTask Start(
    Action<ProgressArgs[]>? progress,
    CancellationToken cancellationToken
  )
  {
    Progress = progress;

    var enumerable = await SourceChannel
      .Reader
      .PipeAsync(1, OnCache, cancellationToken: cancellationToken)
      .Batch(_settings.MaxObjectRequestSize)
      .WithTimeout(TimeSpan.FromMilliseconds(_settings.BatchWaitMilliseconds))
      .PipeAsync(_settings.MaxDownloadThreads, OnTransport, cancellationToken: cancellationToken)
      .Join()
      .ReadAllConcurrentlyAsync(_settings.MaxDeserializeThreads, OnDeserialize, cancellationToken: cancellationToken)
      .ConfigureAwait(false);
  }

  private async ValueTask<string?> OnCache(string id)
  {
    if (CachingStage.Cache.TryGetValue(id, out var @base))
    {
      await ReceiveChannel.Writer.WriteAsync(@base).ConfigureAwait(false);
      return null;
    }
    InvokeProgress();
    return id;
  }

  private async ValueTask<List<Downloaded>> OnTransport(List<string?> b)
  {
    var batch = new List<string>(b.Count);
    foreach (var item in b)
    {
      if (item is not null)
      {
        batch.Add(item);
      }
    }
    var gathered = ServerApiStage.Execute(batch).ConfigureAwait(false);
    var ret = new List<Downloaded>();
    await foreach (var arg in gathered)
    {
      if (!_idToBaseCache.ContainsKey(arg.Id))
      {
        ret.Add(arg);
      }
    }
    InvokeProgress();
    return ret;
  }

  private async ValueTask OnDeserialize(Downloaded transported)
  {
    var deserialized = await DeserializeStage.Execute(transported).ConfigureAwait(false);
    if (deserialized is null)
    {
      return;
    }
    InvokeProgress();
    _idToBaseCache.TryAdd(deserialized.Id, deserialized.BaseObject);
    await ReceiveChannel.Writer.WriteAsync(deserialized.BaseObject).ConfigureAwait(false);
  }

  public CachingStage CachingStage { get; }
  public ServerApiStage ServerApiStage { get; }
  public DeserializeStage DeserializeStage { get; }

  public void Dispose() => ServerApiStage.Dispose();
}
