using System.Threading.Channels;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Serialisation;

public record Transported(string Id, string Json);
public class TransportStage : Stage<string, Transported>
{ 
  private readonly ITransport _transport;
  public TransportStage(ITransport transport)
    : base(Channel.CreateUnbounded<string>())
  {
    _transport = transport;
  }

  protected override async ValueTask<Transported?> Execute(string id)
  {
    var json = await _transport.GetObject(id).ConfigureAwait(false);
    if (json == null)
    {
      return null;
    }

    return new(id, json);
  }
}
