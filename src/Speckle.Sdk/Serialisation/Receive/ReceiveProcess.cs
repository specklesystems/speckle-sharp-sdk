using System.Collections.Concurrent;
using System.Threading.Channels;
using Open.ChannelExtensions;
using Speckle.Sdk.Common;
using Speckle.Sdk.Models;
using Speckle.Sdk.Transports;
using Speckle.Sdk.Transports.ServerUtils;

namespace Speckle.Sdk.Serialisation.Receive;

public record ReceiveProcessSettings(
  int MaxDownloadThreads = 4,
  int MaxDeserializeThreads = 4,
  int MaxObjectRequestSize = ServerApi.BATCH_SIZE_GET_OBJECTS,
  int BatchWaitMilliseconds = 500
);

public sealed class ReceiveProcess : IDisposable
{
  private readonly ReceiveProcessSettings _settings = new();
  private readonly ConcurrentDictionary<string, Base> _idToBaseCache = new(StringComparer.Ordinal);

  private long _received;
  private readonly HashSet<string> _requestedIds = new();
  private Base? _last;

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
    CachingStage = new(_idToBaseCache);
    TransportStage = new TransportStage(baseUri, streamId, authorizationToken);
    DeserializeStage = new() { CachingStage = CachingStage, SourceChannel = SourceChannel };
  }

  public Channel<string> SourceChannel { get; }

  public Action<ProgressArgs[]>? Progress { get; set; }

  public void InvokeProgress() =>
    Progress?.Invoke(
      [
        new ProgressArgs(ProgressEvent.DownloadBytes, TransportStage.Downloaded, null),
        new ProgressArgs(ProgressEvent.DeserializeObject, DeserializeStage.Deserialized, null),
        new ProgressArgs(ProgressEvent.DownloadObject, _received, null),
      ]
    );

  public async Task<Base> GetObject(
    string initialId,
    Action<ProgressArgs[]>? progress,
    CancellationToken cancellationToken
  )
  {
    Progress = progress;

    await SourceChannel.Writer.WriteAsync(initialId, cancellationToken).ConfigureAwait(false);

    var count = await SourceChannel
      .Reader.Pipe(1, OnCache, cancellationToken: cancellationToken)
      .Batch(_settings.MaxObjectRequestSize)
      .WithTimeout(TimeSpan.FromMilliseconds(_settings.BatchWaitMilliseconds))
      .PipeAsync(_settings.MaxDownloadThreads, OnTransport, cancellationToken: cancellationToken)
      .Join()
      .PipeAsync(_settings.MaxDeserializeThreads, OnDeserialize, cancellationToken: cancellationToken)
      .ReadAllAsync(async x => await OnReceive(x, initialId).ConfigureAwait(false), cancellationToken)
      .ConfigureAwait(false);

    Console.WriteLine($"Really Done? {count} {_idToBaseCache.Count}");
    return _last.NotNull();
  }

  private string? OnCache(string id)
  {
    if (CachingStage.Cache.ContainsKey(id))
    {
      return null;
    }

    _received++;
    InvokeProgress();
    return id;
  }

  private async ValueTask<List<Transported>> OnTransport(List<string?> b)
  {
    var batch = new List<string>(b.Count);
    foreach (var item in b)
    {
      if (item is not null)
      {
        batch.Add(item);
      }
    }
    var gathered = TransportStage.Execute(batch).ConfigureAwait(false);
    var ret = new List<Transported>();
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

  private async ValueTask<Deserialized?> OnDeserialize(Transported transported)
  {
    var deserialized = await DeserializeStage.Execute(transported).ConfigureAwait(false);
    if (deserialized is null)
    {
      return null;
    }
    InvokeProgress();
    if (_idToBaseCache.TryAdd(deserialized.Id, deserialized.BaseObject))
    {
      return deserialized;
    }
    return null;
  }

  private async ValueTask OnReceive(Deserialized? received, string initialId)
  {
    if (received is null)
    {
      return;
    }

    if (_requestedIds.Add(received.Id))
    {
      _received++;
    }

    InvokeProgress();
    //Console.WriteLine($"Received {received.Id} - r {_received} - d {_deserialized} - g {_saved} - t {_downloaded}");
    if (received.Id == initialId)
    {
      await SourceChannel.CompleteAsync().ConfigureAwait(false);
      _last = received.BaseObject;
    }
  }

  public CachingStage CachingStage { get; }
  public TransportStage TransportStage { get; }
  public DeserializeStage DeserializeStage { get; }

  public void Dispose() => TransportStage.Dispose();
}
