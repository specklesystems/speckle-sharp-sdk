using System.Collections.Concurrent;
using System.Threading.Channels;
using Open.ChannelExtensions;
using Speckle.Sdk.Common;
using Speckle.Sdk.Models;

namespace Speckle.Sdk.Serialisation;

public record Received(string Id, Base Object);

public sealed class ReceiveStage : IDisposable
{
  private readonly ConcurrentDictionary<string, Base> _idToBaseCache = new(StringComparer.Ordinal);

  private readonly Channel<Deserialized> _channel;

  private  long _deserialized;
  private  long _gathered;
  public ReceiveStage(Uri baseUri, string streamId, string? authorizationToken)
  {
    _channel = Channel.CreateUnbounded<Deserialized>();
    TransportStage = new TransportStage(baseUri, streamId, authorizationToken);
    DeserializeStage = new()
    {
      ReceiveStage = this
    };
  }
  
  public async Task<Base> GetObject(string id)
  {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
    Task.Run(() => TransportStage.Run(OnTransported));    
    Task.Run(() => DeserializeStage.Run(OnDeserialized));
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
    await TransportStage.Writer.WriteAsync(id).ConfigureAwait(false);
    Base? b = null;
    await _channel.Reader.ReadAllAsync((received, index) =>
    {
      if (received.Id == id)
      {
        Console.WriteLine("Done?");
        b = received.BaseObject;
      } 
      else
      {
        static string Process(IStageProcess stage)
        {
          return $"Done: {stage.Done} Queued: {stage.Queued} Dequeued: {stage.Dequeued}";
        }
        Console.WriteLine($"Received {received.Id} - r {index} - d {_deserialized} - g {_gathered} - Transport {Process(TransportStage)}  - Deserialization {Process(DeserializeStage)}");
      }
      return new ValueTask(Task.CompletedTask);
    }).ConfigureAwait(false);
    Console.WriteLine("Really Done?");
    return b.NotNull();
  }

  public TransportStage TransportStage{ get; }
  public DeserializeStage DeserializeStage { get; }

  public IReadOnlyDictionary<string, Base> Cache => _idToBaseCache;
  private async ValueTask OnDeserialized(IReadOnlyList<Deserialized> batch)
  {
    foreach (var arg in batch)
    {
      if (_idToBaseCache.TryAdd(arg.Id, arg.BaseObject))
      {
        _deserialized++;
        await _channel.Writer.WriteAsync(arg).ConfigureAwait(false);
      }
      else
      {
        continue;
      }
    }
  }

  private async ValueTask OnTransported(IReadOnlyList<Transported> batch)
  {
    foreach (var arg in batch)
    {
      if (!_idToBaseCache.ContainsKey(arg.Id))
      {
        _gathered++;
        await DeserializeStage.Writer.WriteAsync(arg).ConfigureAwait(false);
      }
    }
  }

  public void Dispose() => TransportStage.Dispose();
}

