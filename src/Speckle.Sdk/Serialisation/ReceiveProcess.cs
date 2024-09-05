using System.Collections.Concurrent;
using System.Threading.Channels;
using Speckle.Sdk.Models;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Serialisation;

public record Received(string Id, Base Object);

public class ReceiveStage : Stage<string, Received>
{
  private readonly ConcurrentDictionary<string, Base> _idToBaseCache = new(StringComparer.Ordinal);

  private readonly TransportStage _transportStage;
  private readonly DeserializeStage _deserializeStage = new();
  
  public ReceiveStage(ITransport transport)
    : base(Channel.CreateUnbounded<string>())
  {
    _transportStage = new TransportStage(transport)
    {
      Produce = OnTransported
    };
    _deserializeStage.Produce = OnDeserialized;
    _deserializeStage.ReceiveStage = this;
  }
  
  public async Task<Base> GetObject(string id)
  {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
    Task.Run(() => _transportStage.Run());    
    Task.Run(() => _transportStage.Run());
    Task.Run(() => _transportStage.Run());
    Task.Run(() => _transportStage.Run());
    Task.Run(() => _deserializeStage.Run());
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
    
    var tcs = new TaskCompletionSource<Base>();
    Produce = received =>
    {
      if (received.Id == id)
      {
        Console.WriteLine("Done?");
        tcs.TrySetResult(received.Object);
      } 
      else
      {
        static string Process(IStageProcess stage)
        {
          return $"Done: {stage.Done} Queued: {stage.Queued} Dequeued: {stage.Dequeued}";
        }
        Console.WriteLine($"Received {received.Id} - {Process(this)} - Transport {Process(_transportStage)}  - Deserialization {Process(_deserializeStage)}");
      }
      return new ValueTask(Task.CompletedTask);
    };
    await WriteToStage(id).ConfigureAwait(false);
    await Run().ConfigureAwait(false);
    var b = await tcs.Task.ConfigureAwait(false);
    Console.WriteLine("Done?");
    return b;
  }

  public IReadOnlyDictionary<string, Base> Cache => _idToBaseCache;
  private async ValueTask OnDeserialized(Deserialized arg)
  {
    if (_idToBaseCache.TryAdd(arg.Id, arg.BaseObject) && Produce is not null)
    {
      await Produce(new (arg.Id, arg.BaseObject)).ConfigureAwait(false);
    }
  }

  private async ValueTask OnTransported(Transported arg)
  {
    if (_idToBaseCache.ContainsKey(arg.Id))
    {
      return;
    }
    await _deserializeStage.WriteToStage(arg).ConfigureAwait(false);
  }

  protected override async ValueTask<Received?> Execute(string message)
  {
    if (_idToBaseCache.TryGetValue(message, out var @base))
    {
      return new(message, @base);
    }
    await _transportStage.WriteToStage(message).ConfigureAwait(false);
    return null;
  }
}

