using System.Threading.Channels;
using Open.ChannelExtensions;
using Speckle.Sdk.Models;
using Speckle.Sdk.Transports;
using Speckle.Sdk.Transports.ServerUtils;

namespace Speckle.Sdk.Serialisation;

public record SendProcessSettings(
  int MaxSerializeThreads = 4,
  int MaxObjectRequestSize = ServerApi.BATCH_SIZE_GET_OBJECTS,
  int BatchWaitMilliseconds = 500
);

public sealed class SendProcess : IDisposable
{
  private readonly SendProcessSettings _settings = new();
  private int _serialized;
  private int _send;

  public SendProcess(Uri baseUri, string streamId, string? authorizationToken)
  {
    SourceChannel = Channel.CreateUnbounded<Base>();
    SendStage = new(baseUri, streamId, authorizationToken);
    SerializeStage = new();
  }

  public Channel<Base> SourceChannel { get; }
  public SerializeStage SerializeStage { get; set; }
  public SendStage SendStage { get; set; }

  public Action<ProgressArgs[]>? Progress { get; set; }

  public void InvokeProgress() => Progress?.Invoke([]);

  public async ValueTask Finish() => await SourceChannel.CompleteAsync().ConfigureAwait(false);

  public async Task<long> Start(Action<ProgressArgs[]>? progress, CancellationToken cancellationToken)
  {
    Progress = progress;

    var count = await SourceChannel
      .PipeAsync(_settings.MaxSerializeThreads, OnSerialize, cancellationToken: cancellationToken)
      .Batch(_settings.MaxObjectRequestSize)
      .WithTimeout(TimeSpan.FromMilliseconds(_settings.BatchWaitMilliseconds))
      .ReadAllAsync(cancellationToken, OnSend)
      .ConfigureAwait(false);

    Console.WriteLine($"Really Done? {count}");
    return count;
  }

  public async Task SaveObject(Base @base, CancellationToken cancellationToken)
  {
    await SourceChannel.Writer.WriteAsync(@base, cancellationToken).ConfigureAwait(false);
  }

  private async ValueTask<Serialized> OnSerialize(Base @base)
  {
    var serialized = await SerializeStage.Execute(@base).ConfigureAwait(false);
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

  public void Dispose() => SendStage.Dispose();
}
