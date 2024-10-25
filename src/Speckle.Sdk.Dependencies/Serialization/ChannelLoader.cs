using Open.ChannelExtensions;

namespace Speckle.Sdk.Dependencies.Serialization;

public abstract class ChannelLoader
{
  private const int HTTP_GET_CHUNK_SIZE = 500;
  private const int MAX_PARALLELISM_HTTP = 4;
  private static readonly TimeSpan HTTP_BATCH_TIMEOUT = TimeSpan.FromSeconds(2);
  private static readonly int MAX_CACHE_PARALLELISM = Environment.ProcessorCount;

  protected async Task GetAndCache(IEnumerable<string> allChildrenIds, CancellationToken cancellationToken = default) =>
    await allChildrenIds
      .ToChannel(cancellationToken: cancellationToken)
      .Pipe(MAX_CACHE_PARALLELISM, CheckCache, cancellationToken: cancellationToken)
      .Filter(x => x is not null)
      .Batch(HTTP_GET_CHUNK_SIZE)
      .WithTimeout(HTTP_BATCH_TIMEOUT)
      .PipeAsync(
        MAX_PARALLELISM_HTTP,
        async x => await DownloadAndCache(x).ConfigureAwait(false),
        -1,
        false,
        cancellationToken
      )
      .Join()
      .ReadAllConcurrently(MAX_CACHE_PARALLELISM, SaveToCache, cancellationToken)
      .ConfigureAwait(false);

  public abstract string? CheckCache(string id);

  public abstract Task<List<(string, string)>> DownloadAndCache(List<string?> ids);

  public abstract void SaveToCache((string, string) x);
}
