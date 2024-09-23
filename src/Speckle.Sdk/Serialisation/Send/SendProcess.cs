using System.Threading.Channels;
using Open.ChannelExtensions;
using Speckle.Sdk.Common;
using Speckle.Sdk.Models;
using Speckle.Sdk.Transports;
using Speckle.Sdk.Transports.ServerUtils;

namespace Speckle.Sdk.Serialisation.Send;

public record SendProcessSettings(
  int MaxSerializeThreads = 4,
  int MaxObjectRequestSize = ServerApi.BATCH_SIZE_GET_OBJECTS,
  int BatchWaitMilliseconds = 500,
  SqliteManagerOptions? SqliteManagerOptions = null
);

public sealed class SendProcess(IModelTarget modelTarget, SendProcessSettings? settings = null) : IDisposable
{
  private readonly SendProcessSettings _settings = new();
  private readonly Channel<Base> _sourceChannel = Channel.CreateUnbounded<Base>();
  private readonly SerializeStage _serializeStage = new(settings?.SqliteManagerOptions ?? new());
  private readonly SendStage _sendStage = new(modelTarget);

  private long _requested;
  private Serialized? _rootObjectSerialized;
  private Base? _rootObject;
  private Action<ProgressArgs[]>? _progress;

  private void InvokeProgress() =>
    _progress?.Invoke(
      [
        new ProgressArgs(ProgressEvent.UploadObject, _requested, null),
        new ProgressArgs(ProgressEvent.SerializeObject, _serializeStage.Serialized, null),
        new ProgressArgs(ProgressEvent.UploadBytes, _sendStage.Sent, null)
      ]
    );

  public async Task<(string rootObjId, IReadOnlyDictionary<string, ObjectReference> convertedReferences)> SaveObject(
    Base rootObject,
    Action<ProgressArgs[]>? progress,
    CancellationToken cancellationToken
  )
  {
    if (_rootObject is not null)
    {
      throw new InvalidOperationException("SaveObject already been started.");
    }
    _rootObject = rootObject;
    _progress = progress;

    var sourceTask = _sourceChannel
      .Pipe(_settings.MaxSerializeThreads, x => OnSerialize(x, cancellationToken), cancellationToken: cancellationToken)
      .Batch(_settings.MaxObjectRequestSize)
      .WithTimeout(TimeSpan.FromMilliseconds(_settings.BatchWaitMilliseconds))
      .ReadAllAsync(cancellationToken, OnSend)
      .ConfigureAwait(false);

    await _sourceChannel.Writer.WriteAsync(rootObject, cancellationToken).ConfigureAwait(false);
    _requested++;
    await sourceTask;
    InvokeProgress();
    return (_rootObjectSerialized.NotNull().Id, _rootObjectSerialized.ConvertedReferences);
  }

  private Serialized? OnSerialize(Base @base, CancellationToken cancellationToken)
  {
    var serialized = _serializeStage.Execute(@base, cancellationToken);
    InvokeProgress();
    return serialized;
  }

  private async ValueTask OnSend(List<Serialized?> serialized)
  {
    var nonNullSerialized = serialized.Where(x => x is not null).Cast<Serialized>().ToList();
    if (nonNullSerialized.Count == 0)
    {
      return;
    }
    await _sendStage.Execute(nonNullSerialized).ConfigureAwait(false);
    InvokeProgress();
    var root = nonNullSerialized.FirstOrDefault(x => x.BaseObject == _rootObject);
    if (root is not null)
    {
      _rootObjectSerialized = root;
      _sourceChannel.Writer.Complete();
    }
  }

  public void Dispose()
  {
    _sendStage.Dispose();
    _serializeStage.Dispose();
  }
}
