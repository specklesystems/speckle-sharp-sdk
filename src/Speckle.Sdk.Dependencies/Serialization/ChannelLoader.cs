using Open.ChannelExtensions;

namespace Speckle.Sdk.Dependencies.Serialization;

public abstract class ChannelLoader<T>
{
  private const int HTTP_GET_CHUNK_SIZE = 500;
  private const int MAX_PARALLELISM_HTTP = 4;
  private static readonly TimeSpan HTTP_BATCH_TIMEOUT = TimeSpan.FromSeconds(2);
  private static readonly int MAX_READ_CACHE_PARALLELISM = Environment.ProcessorCount;
  private const int MAX_SAVE_CACHE_BATCH = 200;
  private const int MAX_SAVE_CACHE_PARALLELISM = 1;

  protected async Task GetAndCache(IEnumerable<string> allChildrenIds, CancellationToken cancellationToken = default) =>
    await allChildrenIds
      .ToChannel(cancellationToken: cancellationToken)
      .Pipe(MAX_READ_CACHE_PARALLELISM, CheckCache, cancellationToken: cancellationToken)
      .Filter(x => x is not null)
      .Batch(HTTP_GET_CHUNK_SIZE)
      .WithTimeout(HTTP_BATCH_TIMEOUT)
      .PipeAsync(MAX_PARALLELISM_HTTP, async x => await Download(x).ConfigureAwait(false), -1, false, cancellationToken)
      .Join()
      .Batch(MAX_SAVE_CACHE_BATCH)
      .WithTimeout(HTTP_BATCH_TIMEOUT)
      .ReadAllConcurrently(MAX_SAVE_CACHE_PARALLELISM, SaveToCache, cancellationToken)
      .ConfigureAwait(false);

  public abstract string? CheckCache(string id);

  public abstract Task<List<T>> Download(List<string?> ids);

  public abstract void SaveToCache(List<T> x);
}
