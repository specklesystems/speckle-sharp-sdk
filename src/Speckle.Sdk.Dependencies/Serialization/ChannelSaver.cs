using System.Threading.Channels;
using Open.ChannelExtensions;

namespace Speckle.Sdk.Dependencies.Serialization;

public readonly record struct BaseItem(string Id, string Json, bool IsEnd = false);

public abstract class ChannelSaver
{
  private const int HTTP_SEND_CHUNK_SIZE = 500;
  private static readonly TimeSpan HTTP_BATCH_TIMEOUT = TimeSpan.FromSeconds(2);
  private const int MAX_PARALLELISM_HTTP = 4;
  
  private static readonly int MAX_CACHE_READ_PARALLELISM = Environment.ProcessorCount;
  private const int MAX_CACHE_WRITE_PARALLELISM = 1;
  private const int MAX_CACHE_BATCH = 100;

  private readonly Channel<BaseItem> _checkCacheChannel = Channel.CreateUnbounded<BaseItem>();

  public Task Start(string streamId, CancellationToken cancellationToken = default) =>
    _checkCacheChannel
      .Pipe(MAX_CACHE_READ_PARALLELISM, CheckCache, -1, false, cancellationToken)
      .Filter(x => x is not null)
      .Batch(HTTP_SEND_CHUNK_SIZE)
      .WithTimeout(HTTP_BATCH_TIMEOUT)
      .PipeAsync(
        MAX_PARALLELISM_HTTP,
        async x => await SendToServer(streamId, x.Cast<BaseItem>().ToList(), cancellationToken).ConfigureAwait(false),
        -1,
        false,
        cancellationToken
      )
      .Join()
      .Batch(MAX_CACHE_BATCH)
      .WithTimeout(HTTP_BATCH_TIMEOUT)
      .ReadAllConcurrently(MAX_CACHE_WRITE_PARALLELISM, SaveToCache, cancellationToken);

  public async Task Save(BaseItem item, CancellationToken cancellationToken = default) =>
    await _checkCacheChannel.Writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);

  public void Done() => _checkCacheChannel.Writer.TryComplete();

  public abstract BaseItem? CheckCache(BaseItem item);

  public abstract Task<List<BaseItem>> SendToServer(
    string streamId,
    List<BaseItem> batch,
    CancellationToken cancellationToken
  );

  public abstract void SaveToCache(List<BaseItem> item);
}
