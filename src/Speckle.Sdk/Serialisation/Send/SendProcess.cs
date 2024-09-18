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
  int BatchWaitMilliseconds = 500
);

public sealed class SendProcess : IDisposable
{
  private readonly SendProcessSettings _settings = new();

  public SendProcess(IModelTarget modelTarget)
  {
    SourceChannel = Channel.CreateUnbounded<Base>();
    SendStage = new(modelTarget);
    SerializeStage = new();
  }

  private long _requested;
  private Serialized? _rootObjectSerialized;
  private Base? _rootObject;
  public Channel<Base> SourceChannel { get; }
  public SerializeStage SerializeStage { get; set; }
  public SendStage SendStage { get; set; }

  public Action<ProgressArgs[]>? Progress { get; set; }

  public void InvokeProgress() =>
    Progress?.Invoke(
      [
        new ProgressArgs(ProgressEvent.UploadObject, _requested, null),
        new ProgressArgs(ProgressEvent.SerializeObject, SerializeStage.Serialized, null),
        new ProgressArgs(ProgressEvent.UploadBytes, SendStage.Sent, null)
      ]
    );

  public async Task<(string rootObjId, IReadOnlyDictionary<string, ObjectReference> convertedReferences)> SaveObject(
    Base rootObject,
    Action<ProgressArgs[]>? progress,
    CancellationToken cancellationToken
  )
  {
    Progress = progress;
    _rootObject = rootObject;

    var sourceTask = SourceChannel
      .Pipe(_settings.MaxSerializeThreads, OnSerialize, cancellationToken: cancellationToken)
      .Batch(_settings.MaxObjectRequestSize)
      .WithTimeout(TimeSpan.FromMilliseconds(_settings.BatchWaitMilliseconds))
      .ReadAllAsync(cancellationToken, OnSend)
      .ConfigureAwait(false);

    await SourceChannel.Writer.WriteAsync(rootObject, cancellationToken).ConfigureAwait(false);
    _requested++;
    await sourceTask;
    InvokeProgress();
    return (_rootObjectSerialized.NotNull().Id, _rootObjectSerialized.ConvertedReferences);
  }

  private Serialized OnSerialize(Base @base)
  {
    var serialized = SerializeStage.Execute(@base);
    InvokeProgress();
    return serialized;
  }

  private async ValueTask OnSend(List<Serialized> serialized)
  {
    await SendStage.Execute(serialized).ConfigureAwait(false);
    InvokeProgress();
    var root = serialized.FirstOrDefault(x => x.BaseObject == _rootObject);
    if (root is not null)
    {
      _rootObjectSerialized = root;
      SourceChannel.Writer.Complete();
    }
  }

  public void Dispose() => SendStage.Dispose();
}
