using System.Threading.Channels;
using Open.ChannelExtensions;
using Speckle.Sdk.Serialisation.V2.Send;

namespace Speckle.Sdk.Dependencies.Serialization;

public abstract class ChannelSaver<T>
  where T : IHasSize
{
  private const int SEND_CAPACITY = 500;
  private const int HTTP_SEND_CHUNK_SIZE = 25_000_000; //bytes
  private static readonly TimeSpan HTTP_BATCH_TIMEOUT = TimeSpan.FromSeconds(2);
  private const int MAX_PARALLELISM_HTTP = 4;
  private const int HTTP_CAPACITY = 500;
  private const int MAX_CACHE_WRITE_PARALLELISM = 4;
  private const int MAX_CACHE_BATCH = 200;

  private readonly Channel<T> _checkCacheChannel = Channel.CreateBounded<T>(
    new BoundedChannelOptions(SEND_CAPACITY)
    {
      AllowSynchronousContinuations = false,
      Capacity = SEND_CAPACITY,
      SingleWriter = false,
      SingleReader = false,
      FullMode = BoundedChannelFullMode.Wait,
    },
    _ => throw new NotImplementedException("Dropping items not supported.")
  );

  public Task Start(CancellationToken cancellationToken = default) =>
    _checkCacheChannel
      .Reader.BatchBySize(HTTP_SEND_CHUNK_SIZE)
      .WithTimeout(HTTP_BATCH_TIMEOUT)
      .PipeAsync(
        MAX_PARALLELISM_HTTP,
        async x => await SendToServer(x, cancellationToken).ConfigureAwait(false),
        HTTP_CAPACITY,
        false,
        cancellationToken
      )
      .Join()
      .Batch(MAX_CACHE_BATCH)
      .WithTimeout(HTTP_BATCH_TIMEOUT)
      .ReadAllConcurrently(MAX_CACHE_WRITE_PARALLELISM, SaveToCache, cancellationToken);

  public ValueTask Save(T item, CancellationToken cancellationToken = default) =>
    _checkCacheChannel.Writer.WriteAsync(item, cancellationToken);

  public abstract Task<List<T>> SendToServer(List<T> batch, CancellationToken cancellationToken);

  public Task Done()
  {
    _checkCacheChannel.Writer.Complete();
    return Task.CompletedTask;
  }

  public abstract void SaveToCache(List<T> item);
}
