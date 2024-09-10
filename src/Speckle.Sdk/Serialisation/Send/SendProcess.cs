using System.Threading.Channels;
using Open.ChannelExtensions;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation.Receive;
using Speckle.Sdk.Transports;
using Speckle.Sdk.Transports.ServerUtils;

namespace Speckle.Sdk.Serialisation;

public record Serialized(string Id, string Json);

public class SerializeStage
{
  public async ValueTask<Serialized?> Execute(Base @base)
  {
    throw new NotImplementedException();
  }
}

public class SendStage
{
  public async ValueTask Execute(List<Serialized> serialized)
  {
    throw new NotImplementedException();
  }
}

public record SendProcessSettings(
  int MaxSerializeThreads = 4,
  int MaxSendingThreads = 4,
  int MaxObjectRequestSize = ServerApi.BATCH_SIZE_GET_OBJECTS,
  int BatchWaitMilliseconds = 500
);

public class SendProcess
{
  private SendProcessSettings _settings = new();
  private int _serialized;
  private int _send;

  public SendProcess(Uri baseUri, string streamId, string? authorizationToken)
  {
    SourceChannel = Channel.CreateUnbounded<Base>();
  }

  public Channel<Base> SourceChannel { get; }
  public SerializeStage SerializeStage { get; set; }
  public SendStage SendStage { get; set; }

  public Action<ProgressArgs[]>? Progress { get; set; }

  public void InvokeProgress() => Progress?.Invoke([]);

  public async Task<string> SaveObject(
    Base @base,
    Action<ProgressArgs[]>? progress,
    CancellationToken cancellationToken
  )
  {
    Progress = progress;

    await SourceChannel.Writer.WriteAsync(@base, cancellationToken).ConfigureAwait(false);

    var count = await SourceChannel
      .PipeAsync(_settings.MaxSerializeThreads, OnSerialize, cancellationToken: cancellationToken)
      .Batch(_settings.MaxObjectRequestSize)
      .WithTimeout(TimeSpan.FromMilliseconds(_settings.BatchWaitMilliseconds))
      .PipeAsync(_settings.MaxSendingThreads, OnSend, cancellationToken: cancellationToken)
      .ReadAllAsync(cancellationToken)
      .ConfigureAwait(false);

    Console.WriteLine($"Really Done? {count} {_idToBaseCache.Count}");
    return _last.NotNull();
  }

  private async ValueTask<Serialized?> OnSerialize(Base @base)
  {
    var serialized = await SerializeStage.Execute(@base).ConfigureAwait(false);
    if (serialized is null)
    {
      return null;
    }
    InvokeProgress();
    _serialized++;
    return serialized;
  }

  private async ValueTask OnSend(List<Serialized> serialized)
  {
    await SendStage.Execute(serialized).ConfigureAwait(false);
    InvokeProgress();
    _send++;
  }
}
