using System.Threading.Channels;
using Open.ChannelExtensions;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation.Receive;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Serialisation;

public record Serialized(string Id, string Json);
public class SerializeStage
{
  public async ValueTask<Serialized?> Execute(Base @base)
  {
    throw new NotImplementedException();
  }
}
public class SerializeStage
{
  public async ValueTask<Serialized?> Execute(Base @base)
  {
    throw new NotImplementedException();
  }
}
public record SendProcessSettings(int MaxSerializeThreads);
public class SendProcess
{
  private int _serialized;
  private SendProcessSettings _settings = new();

  public SendProcess(Uri baseUri, string streamId, string? authorizationToken)
  {
    SourceChannel = Channel.CreateUnbounded<string>();
  }

  public Channel<string> SourceChannel { get; }
  public SerializeStage SerializeStage { get; set; }

  public Action<ProgressArgs[]>? Progress { get; set; }

  public void InvokeProgress() =>
    Progress?.Invoke([]);

  public async Task<string> SaveObject(
    string json,
    Action<ProgressArgs[]>? progress,
    CancellationToken cancellationToken
  )
  {
    Progress = progress;

    await SourceChannel.Writer.WriteAsync(json, cancellationToken).ConfigureAwait(false);

    var count = await SourceChannel
      .PipeAsync(_settings.MaxSerializeThreads, OnSerialize, cancellationToken: cancellationToken)
      .PipeAsync(_settings.MaxDeserializeThreads, OnDeserialize, cancellationToken: cancellationToken)
      .ReadAllAsync(async x => await OnReceive(x, initialId).ConfigureAwait(false), cancellationToken)
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
    var serialized = await SerializeStage.Execute(@base).ConfigureAwait(false);
    if (serialized is null)
    {
      return null;
    }
    InvokeProgress();
    _serialized++;
    return serialized;
  }
}
