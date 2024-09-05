using System.Collections.Concurrent;
using System.Threading.Channels;
using Speckle.Sdk.Models;

namespace Speckle.Sdk.Serialisation;

public record Received(string Id, Base Object);

public class ReceiveStage : Stage<string, Received>
{
  private readonly ConcurrentDictionary<string, Base> _idToBaseCache = new(StringComparer.Ordinal);

  private readonly TransportStage _transportStage = new();
  private readonly DeserializeStage _deserializeStage = new();
  
  public ReceiveStage(Func<Received, ValueTask> onReceive)
    : base(Channel.CreateUnbounded<string>())
  {
    Produce = onReceive;
    _transportStage.Produce = OnTransported;
    _deserializeStage.Produce = OnDeserialized;
    Task.Run(() => Run());
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

