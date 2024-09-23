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
  private bool _isCompleted;

  private readonly Channel<string> _sourceChannel;
  private readonly Channel<string> _downloadChannel;
  private readonly Channel<Downloaded> _deserializeChannel;
  private readonly CachingStage _cachingStage;
  private readonly DownloadStage _downloadStage;
  private readonly DeserializeStage _deserializeStage;

  public ReceiveProcess(IModelSource modelSource, ReceiveProcessSettings? settings = null)
  {
    if (settings is not null)
    {
      _settings = settings;
    }
    _sourceChannel = Channel.CreateUnbounded<string>();
    _downloadChannel = Channel.CreateUnbounded<string>();
    _deserializeChannel = Channel.CreateUnbounded<Downloaded>();
    _cachingStage = new(
      _settings.SqliteManagerOptions ?? new SqliteManagerOptions(),
      SendToDownload,
      SendToDeserialize
    );
    _downloadStage = new(SendToDeserialize, modelSource);
    _deserializeStage = new(_idToBaseCache, SendToCheckCache, Done, _settings.DeserializedOptions);
  }

  private long _bytes;
  public Action<ProgressArgs[]>? Progress { get; set; }

  public void InvokeProgress() =>
    Progress?.Invoke(
      [
        new ProgressArgs(ProgressEvent.DownloadBytes, _bytes, null),
        new ProgressArgs(ProgressEvent.DeserializeObject, _deserializeStage.Deserialized, null)
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
      await _sourceChannel.Writer.WriteAsync(id, cancellationToken).ConfigureAwait(false);
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
    await _downloadChannel.Writer.WriteAsync(id, cancellationToken).ConfigureAwait(false);
    InvokeProgress();
  }

  private async ValueTask SendToDeserialize(Downloaded id, CancellationToken cancellationToken)
  {
    if (_isCompleted)
    {
      return;
    }
    await _deserializeChannel.Writer.WriteAsync(id, cancellationToken).ConfigureAwait(false);
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
    _sourceChannel.Writer.Complete();
    _downloadChannel.Writer.Complete();
    _deserializeChannel.Writer.Complete();
  }

  public void Dispose()
  {
    _downloadStage.Dispose();
    _cachingStage.Dispose();
  }

  public async ValueTask<Base> GetObject(
    string objectId,
    Action<ProgressArgs[]>? progress,
    CancellationToken cancellationToken
  )
  {
    Progress = progress;
    _rootObjectId = objectId;

    var pipelineTask = _sourceChannel
      .Reader.Batch(_settings.MaxObjectRequestSize)
      .WithTimeout(TimeSpan.FromMilliseconds(_settings.BatchWaitMilliseconds))
      .ReadAllConcurrentlyAsync(
        _settings.MaxDownloadThreads,
        x => _cachingStage.Execute(x, cancellationToken),
        cancellationToken: cancellationToken
      );

    var downloadTask = _downloadChannel
      .Reader.Batch(_settings.MaxObjectRequestSize)
      .WithTimeout(TimeSpan.FromMilliseconds(_settings.BatchWaitMilliseconds))
      .ReadAllConcurrentlyAsync(
        _settings.MaxDownloadThreads,
        x =>
          _downloadStage.Execute(
            x,
            args =>
            {
              _bytes += args.Count ?? 0;
              InvokeProgress();
            },
            cancellationToken
          ),
        cancellationToken
      );

    var deserializeTask = _deserializeChannel.ReadAllConcurrentlyAsync(
      _settings.MaxDeserializeThreads,
      x => _deserializeStage.Execute(x, cancellationToken),
      cancellationToken: cancellationToken
    );

    await SendToCheckCache(_rootObjectId, cancellationToken).ConfigureAwait(false);
    await pipelineTask.ConfigureAwait(false);
    await downloadTask.ConfigureAwait(false);
    await deserializeTask.ConfigureAwait(false);
    return _rootObject.NotNull();
  }
}
