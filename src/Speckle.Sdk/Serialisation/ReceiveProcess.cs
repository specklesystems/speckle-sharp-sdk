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
  private long _transported;
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
      .PipeFilter(out var cached, 1, OnFilterOutCached)
      .Batch(ServerApi.BATCH_SIZE_GET_OBJECTS)
      .WithTimeout(TimeSpan.FromMilliseconds(500))
      .PipeAsync(4, OnTransport)
      .Join()
      .PipeAsync(2, OnDeserialize)
      .ReadAll(async x => await OnReceive(x, initialId).ConfigureAwait(false)).ConfigureAwait(false);
    var unmatched = await cached.ReadAll(x => { }).ConfigureAwait(false);
    
    Console.WriteLine($"Really Done? {count} {unmatched}");
    return _last.NotNull();
  }

  private bool OnFilterOutCached(string id) => !_idToBaseCache.ContainsKey(id);

  private async ValueTask<List<Transported>> OnTransport(List<string> batch)
  {
    var gathered = await TransportStage.Execute(batch).ConfigureAwait(false);
    var ret = new List<Transported>(gathered.Count);
    foreach (var arg in gathered)
    {
      _transported++;
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
    _deserialized++;
    if (_idToBaseCache.TryAdd(deserialized.Id, deserialized.BaseObject))
    {
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

    Console.WriteLine($"Received {received.Id} - r {_received} - d {_deserialized} - g {_gathered} - t {_transported}");
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

