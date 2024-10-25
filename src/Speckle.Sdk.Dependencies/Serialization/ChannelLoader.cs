using Open.ChannelExtensions;

namespace Speckle.Sdk.Serialisation.V2.Receive;

public abstract class ChannelLoader
{
  private const int HTTP_ID_CHUNK_SIZE = 500;
  private const int MAX_PARALLELISM_HTTP = 4;

  protected async Task GetAndCache(IEnumerable<string> allChildrenIds, CancellationToken cancellationToken = default) =>
    await allChildrenIds
      .ToChannel(cancellationToken: cancellationToken)
      .Pipe(Environment.ProcessorCount, CheckCache, cancellationToken: cancellationToken)
      .Filter(x => x is not null)
      .Batch(HTTP_ID_CHUNK_SIZE)
      .WithTimeout(TimeSpan.FromSeconds(2))
      .PipeAsync(
        MAX_PARALLELISM_HTTP,
        async x => await DownloadAndCache(x).ConfigureAwait(false),
        -1,
        false,
        cancellationToken
      )
      .Join()
      .ReadAllConcurrently(Environment.ProcessorCount, SaveToCache, cancellationToken)
      .ConfigureAwait(false);

  public abstract string? CheckCache(string id);

  public abstract Task<List<(string, string)>> DownloadAndCache(List<string?> ids);

  public abstract void SaveToCache((string, string) x);
}
