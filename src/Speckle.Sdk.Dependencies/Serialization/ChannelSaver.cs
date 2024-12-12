using System.Threading.Channels;
using Open.ChannelExtensions;
using Speckle.Sdk.Serialisation.V2.Send;

namespace Speckle.Sdk.Dependencies.Serialization;


public abstract class ChannelSaver<T>
where T : IHasSize
{
  private const int SEND_CAPACITY = 50;
  private const int HTTP_SEND_CHUNK_SIZE = 25_000_000; //bytes
  private static readonly TimeSpan HTTP_BATCH_TIMEOUT = TimeSpan.FromSeconds(2);
  private const int MAX_PARALLELISM_HTTP = 4;
  private const int HTTP_CAPACITY = 50;
  private const int MAX_CACHE_WRITE_PARALLELISM = 1;
  private const int MAX_CACHE_BATCH = 200;
  
  private bool _enabled;

  private readonly Channel<T> _checkCacheChannel = Channel.CreateBounded<T>(
    new BoundedChannelOptions(SEND_CAPACITY)
    {
      AllowSynchronousContinuations = true,
      Capacity = SEND_CAPACITY,
      SingleWriter = false,
      SingleReader = false,
      FullMode = BoundedChannelFullMode.Wait,
    },
    _ => throw new NotImplementedException("Dropping items not supported.")
  );

  public Task<long> Start(bool enableServerSending = true, bool enableCacheSaving = true, CancellationToken cancellationToken = default)
  {
    ValueTask<long> t = new(Task.FromResult(0L));
    if (enableServerSending)
    {
      _enabled = true;
      var tChannelReader = _checkCacheChannel
        .Reader.BatchBySize(HTTP_SEND_CHUNK_SIZE)
        .WithTimeout(HTTP_BATCH_TIMEOUT)
        .PipeAsync(
          MAX_PARALLELISM_HTTP,
          async x => await SendToServer(x, cancellationToken).ConfigureAwait(false),
          HTTP_CAPACITY,
          false,
          cancellationToken
        );
      if (enableCacheSaving)
      {
        t =new (tChannelReader.Join()
          .Batch(MAX_CACHE_BATCH)
          .WithTimeout(HTTP_BATCH_TIMEOUT)
          .ReadAllConcurrently(MAX_CACHE_WRITE_PARALLELISM, SaveToCache, cancellationToken));
      }
      else
      {
        t = tChannelReader.ReadUntilCancelledAsync(cancellationToken, (list, l) => new ValueTask());
      }
    }

    return t.AsTask();
  }

  public async ValueTask Save(T item, CancellationToken cancellationToken = default)
  {
    if (_enabled)
    {
      await _checkCacheChannel.Writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);
    }
  }

  public abstract Task<List<T>> SendToServer(List<T> batch, CancellationToken cancellationToken);

  public ValueTask Done()
  {
    _checkCacheChannel.Writer.Complete();
    return new(Task.CompletedTask);
  }

  public abstract void SaveToCache(List<T> item);
}
