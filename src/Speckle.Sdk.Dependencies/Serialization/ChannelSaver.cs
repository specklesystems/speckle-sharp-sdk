using System.Text;
using System.Threading.Channels;
using Open.ChannelExtensions;
using Speckle.Sdk.Serialisation.V2.Send;

namespace Speckle.Sdk.Dependencies.Serialization;

public readonly record struct BaseItem(string Id, string Json, bool NeedsStorage)
{
  public int Size { get; } = Encoding.UTF8.GetByteCount(Json);

  public bool Equals(BaseItem? other)
  {
    if (other is null)
    {
      return false;
    }
    return string.Equals(Id, other.Value.Id, StringComparison.OrdinalIgnoreCase);
  }

  public override int GetHashCode() => Id.GetHashCode();
}

public abstract class ChannelSaver
{
  private const int SEND_CAPACITY = 50;
  private const int HTTP_SEND_CHUNK_SIZE = 25_000_000; //bytes
  private static readonly TimeSpan HTTP_BATCH_TIMEOUT = TimeSpan.FromSeconds(2);
  private const int MAX_PARALLELISM_HTTP = 4;
  private const int HTTP_CAPACITY = 50;
  private const int MAX_CACHE_WRITE_PARALLELISM = 1;
  private const int MAX_CACHE_BATCH = 200;

  private readonly Channel<BaseItem> _checkCacheChannel = Channel.CreateBounded<BaseItem>(
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

  public Task Start(CancellationToken cancellationToken = default)
  {
    var t = _checkCacheChannel
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
    return t;
  }

  public async Task Save(BaseItem item, CancellationToken cancellationToken = default) =>
    await _checkCacheChannel.Writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);

  public abstract Task<List<BaseItem>> SendToServer(List<BaseItem> batch, CancellationToken cancellationToken);

  public Task Done()
  {
    _checkCacheChannel.Writer.Complete();
    return Task.CompletedTask;
  }

  public abstract void SaveToCache(List<BaseItem> item);
}
