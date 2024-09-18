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
  int MaxObjectRequestSize = ServerApi.BATCH_SIZE_GET_OBJECTS - 1,
  int BatchWaitMilliseconds = 500,
  SqliteManagerOptions? SqliteManagerOptions = null,
  DeserializedOptions? DeserializedOptions = null
);

public sealed class ReceiveProcess : IDisposable
{
  private readonly ReceiveProcessSettings _settings = new();
  private readonly ConcurrentDictionary<string, Base> _idToBaseCache = new(StringComparer.Ordinal);

  private string? _rootObjectId;
  private Base? _rootObject;

  public ReceiveProcess(IModelSource modelSource, ReceiveProcessSettings? settings = null)
  {
    if (settings is not null)
    {
      _settings = settings;
    }
    SourceChannel = Channel.CreateUnbounded<string>();
    DownloadChannel = Channel.CreateUnbounded<string>();
    DeserializeChannel = Channel.CreateUnbounded<Downloaded>();
    CachingStage = new(_settings.SqliteManagerOptions ?? new SqliteManagerOptions(), SendToDownload, SendToDeserialize);
    DownloadStage = new(SendToDeserialize, modelSource);
    DeserializeStage = new(_idToBaseCache, SendToCheckCache, Done, _settings.DeserializedOptions);
  }

  private bool _isCompleted;

  private Channel<string> SourceChannel { get; }
  private Channel<string> DownloadChannel { get; }
  private Channel<Downloaded> DeserializeChannel { get; }
  private CachingStage CachingStage { get; }
  private DownloadStage DownloadStage { get; }
  private DeserializeStage DeserializeStage { get; }

  private long _bytes;
  public Action<ProgressArgs[]>? Progress { get; set; }

  public void InvokeProgress() =>
    Progress?.Invoke(
      [
        new ProgressArgs(ProgressEvent.DownloadBytes, _bytes, null),
        new ProgressArgs(ProgressEvent.DeserializeObject, DeserializeStage.Deserialized, null)
      ]
    );

  private async ValueTask SendToCheckCache(string id, CancellationToken cancellationToken)
  {
    if (_isCompleted)
    {
      return;
    }
    if (_settings.SqliteManagerOptions?.Enabled ?? true)
    {
      await SourceChannel.Writer.WriteAsync(id, cancellationToken).ConfigureAwait(false);
      InvokeProgress();
    }
    else
    {
      await SendToDownload(id, cancellationToken).ConfigureAwait(false);
    }
  }

  private async ValueTask SendToDownload(string id, CancellationToken cancellationToken)
  {
    if (_isCompleted)
    {
      return;
    }
    await DownloadChannel.Writer.WriteAsync(id, cancellationToken).ConfigureAwait(false);
    InvokeProgress();
  }

  private async ValueTask SendToDeserialize(Downloaded id, CancellationToken cancellationToken)
  {
    if (_isCompleted)
    {
      return;
    }
    await DeserializeChannel.Writer.WriteAsync(id, cancellationToken).ConfigureAwait(false);
    InvokeProgress();
  }

  private void Done(Deserialized deserialized)
  {
    if (_isCompleted)
    {
      return;
    }
    InvokeProgress();
    _idToBaseCache.TryAdd(deserialized.Id, deserialized.BaseObject);
    if (deserialized.Id == _rootObjectId)
    {
      _rootObject = deserialized.BaseObject;
      Complete();
    }
  }

  private void Complete()
  {
    _isCompleted = true;
    SourceChannel.Writer.Complete();
    DownloadChannel.Writer.Complete();
    DeserializeChannel.Writer.Complete();
  }

  public void Dispose() => DownloadStage.Dispose();

  public async ValueTask<Base> GetObject(
    string objectId,
    Action<ProgressArgs[]>? progress,
    CancellationToken cancellationToken
  )
  {
    Progress = progress;
    _rootObjectId = objectId;

    var pipelineTask = SourceChannel
      .Reader.Batch(_settings.MaxObjectRequestSize)
      .WithTimeout(TimeSpan.FromMilliseconds(_settings.BatchWaitMilliseconds))
      .ReadAllConcurrentlyAsync(
        _settings.MaxDownloadThreads,
        x => CachingStage.Execute(x, cancellationToken),
        cancellationToken: cancellationToken
      );

    var downloadTask = DownloadChannel
      .Reader.Batch(_settings.MaxObjectRequestSize)
      .WithTimeout(TimeSpan.FromMilliseconds(_settings.BatchWaitMilliseconds))
      .ReadAllConcurrentlyAsync(
        _settings.MaxDownloadThreads,
        x =>
          DownloadStage.Execute(
            x,
            args =>
            {
              _bytes += args.Count ?? 0;
              InvokeProgress();
            }, cancellationToken
          ),
        cancellationToken
      );

    var deserializeTask = DeserializeChannel.ReadAllConcurrentlyAsync(
      _settings.MaxDeserializeThreads,
      x => DeserializeStage.Execute(x, cancellationToken),
      cancellationToken: cancellationToken
    );

    await SendToCheckCache(_rootObjectId, cancellationToken).ConfigureAwait(false);
    await pipelineTask.ConfigureAwait(false);
    await downloadTask.ConfigureAwait(false);
    await deserializeTask.ConfigureAwait(false);
    return _rootObject.NotNull();
  }
}
