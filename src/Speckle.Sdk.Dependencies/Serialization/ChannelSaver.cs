using System.Text;
using System.Threading.Channels;
using Open.ChannelExtensions;
using Speckle.Sdk.Serialisation.V2.Send;

namespace Speckle.Sdk.Dependencies.Serialization;

public readonly record struct BaseItem(string Id, string Json, bool NeedsStorage)
{
  public int Size { get; } = Encoding.UTF8.GetByteCount(Json);
}

public abstract class ChannelSaver
{
  private const int HTTP_SEND_CHUNK_SIZE = 25_000_000; //bytes
  private static readonly TimeSpan HTTP_BATCH_TIMEOUT = TimeSpan.FromSeconds(2);
  private const int MAX_PARALLELISM_HTTP = 4;
  private const int MAX_CACHE_WRITE_PARALLELISM = 1;
  private const int MAX_CACHE_BATCH = 200;
  private const string DUMMY = "dummy";

  private readonly Channel<BaseItem> _checkCacheChannel = Channel.CreateUnbounded<BaseItem>();

  public Task Start(CancellationToken cancellationToken = default)
  {
    var t = _checkCacheChannel
      .Reader.BatchBySize(HTTP_SEND_CHUNK_SIZE)
      .WithTimeout(HTTP_BATCH_TIMEOUT)
      .PipeAsync(
        MAX_PARALLELISM_HTTP,
        async x => await SendToServerInternal(x, cancellationToken).ConfigureAwait(false),
        -1,
        false,
        cancellationToken
      )
      .Join()
      .Batch(MAX_CACHE_BATCH)
      .WithTimeout(HTTP_BATCH_TIMEOUT)
      .ReadAllConcurrently(MAX_CACHE_WRITE_PARALLELISM, SaveToCacheInternal, cancellationToken);
    return t;
  }

  public async Task Save(BaseItem item, CancellationToken cancellationToken = default) =>
    await _checkCacheChannel.Writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);

  private async Task<List<BaseItem>> SendToServerInternal(
    List<BaseItem> batch,
    CancellationToken cancellationToken = default
  )
  {
    var ending = batch.Select(x => x.Id).Contains(DUMMY);
    if (ending)
    {
      batch.RemoveAll(x => x.Id == DUMMY);
    }
    var results = await SendToServer(batch, cancellationToken).ConfigureAwait(false);
    if (ending)
    {
      results.Add(new BaseItem(DUMMY, DUMMY, false));
    }
    return results;
  }

  public abstract Task<List<BaseItem>> SendToServer(List<BaseItem> batch, CancellationToken cancellationToken);

  public async Task Done() => await Save(new BaseItem(DUMMY, DUMMY, false)).ConfigureAwait(false);

  private void SaveToCacheInternal(List<BaseItem> batch)
  {
    var ending = batch.Select(x => x.Id).Contains(DUMMY);
    if (ending)
    {
      batch.RemoveAll(x => x.Id == DUMMY);
    }
    SaveToCache(batch);
    if (ending)
    {
      _checkCacheChannel.Writer.Complete();
    }
  }

  public abstract void SaveToCache(List<BaseItem> item);
}
