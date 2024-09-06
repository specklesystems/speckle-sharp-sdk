using System.Collections.Concurrent;
using System.Threading.Channels;
using Open.ChannelExtensions;
using Speckle.Sdk.Common;
using Speckle.Sdk.Models;
using Speckle.Sdk.Transports.ServerUtils;

namespace Speckle.Sdk.Serialisation;


public sealed class ReceiveStage : IDisposable
{
  private readonly ConcurrentDictionary<string, Base> _idToBaseCache = new(StringComparer.Ordinal);

  private  long _deserialized;
  private  long _gathered;
  private long _received;
  private HashSet<string> _requestedIds = new();
  private Base? _last;
  public ReceiveStage(Uri baseUri, string streamId, string? authorizationToken)
  {
    TransportStage = new TransportStage(baseUri, streamId, authorizationToken);
    DeserializeStage = new()
    {
      ReceiveStage = this
    };
  }
  
  public Channel<string> SourceChannel { get; private set; }
  
  public async Task<Base> GetObject(string initialId)
  {
    SourceChannel = Channel.CreateUnbounded<string>();
    
    await SourceChannel.Writer.WriteAsync(initialId).ConfigureAwait(false);
    
      
    var count = await SourceChannel.Reader
      .Batch(ServerApi.BATCH_SIZE_GET_OBJECTS)
      .WithTimeout(TimeSpan.FromMilliseconds(500))
      .PipeAsync(4, OnTransport)
      .Join()
      .PipeAsync(2, OnDeserialize)
      .ReadAll(async x => await OnReceive(x, initialId).ConfigureAwait(false)).ConfigureAwait(false);
    Console.WriteLine($"Really Done? {count}");
    return _last.NotNull();
  }

  private async ValueTask<List<Transported>> OnTransport(List<string> batch)
  {
    var gathered = await TransportStage.Execute(batch).ConfigureAwait(false);
    var ret = new List<Transported>(gathered.Count);
    foreach (var arg in gathered)
    {
      if (!_idToBaseCache.ContainsKey(arg.Id))
      {
        _gathered++;
        ret.Add(arg);
      }
    }
    return ret;
  }

  private async ValueTask<Deserialized?> OnDeserialize(Transported transported)
  {
    var deserialized = await DeserializeStage.Execute(transported).ConfigureAwait(false);
    if (deserialized is null)
    {
      return null;
    }
    if (_idToBaseCache.TryAdd(deserialized.Id, deserialized.BaseObject))
    {
      _deserialized++;
      return deserialized;
    }

    return null;
  }

  private async ValueTask OnReceive(Deserialized? received, string initialId)
  {
    if (received is null)
    {
      return;
    }

    if (_requestedIds.Add(received.Id))
    {
      _received++;
    }

    Console.WriteLine($"Received {received.Id} - r {_received} - d {_deserialized} - g {_gathered}");
    if (received.Id == initialId)
    {
      await SourceChannel.CompleteAsync().ConfigureAwait(false);
      _last = received.BaseObject;
    }
  }

  public TransportStage TransportStage{ get; }
  public DeserializeStage DeserializeStage { get; }

  public IReadOnlyDictionary<string, Base> Cache => _idToBaseCache;
  
  public void Dispose() => TransportStage.Dispose();
}

