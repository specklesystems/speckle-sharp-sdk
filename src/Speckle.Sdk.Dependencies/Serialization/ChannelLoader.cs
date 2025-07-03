using System.Threading.Channels;
using Open.ChannelExtensions;

namespace Speckle.Sdk.Dependencies.Serialization;

public abstract class ChannelLoader<T>(CancellationToken cancellationToken)
{
  private const int RECEIVE_CAPACITY = 10000;

  private const int HTTP_GET_CHUNK_SIZE = 500;
  private const int MAX_PARALLELISM_HTTP = 4;
  private static readonly TimeSpan HTTP_BATCH_TIMEOUT = TimeSpan.FromSeconds(2);
  private const int MAX_SAVE_CACHE_BATCH = 500;
  private const int MAX_SAVE_CACHE_PARALLELISM = 4;

  private readonly CancellationTokenSource _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

  private readonly Channel<string> _channel = Channel.CreateBounded<string>(
    new BoundedChannelOptions(RECEIVE_CAPACITY)
    {
      AllowSynchronousContinuations = true,
      Capacity = RECEIVE_CAPACITY,
      SingleWriter = false,
      SingleReader = false,
      FullMode = BoundedChannelFullMode.Wait,
    },
    _ => throw new NotImplementedException("Dropping items not supported.")
  );

  protected async Task GetAndCache(IEnumerable<string> allChildrenIds, int? maxParallelism = null) =>
    await _channel
      .Source(allChildrenIds, _cts.Token)
      .Pipe(maxParallelism ?? Environment.ProcessorCount, CheckCache, cancellationToken: _cts.Token)
      .Filter(x => x is not null)
      .Batch(HTTP_GET_CHUNK_SIZE)
      .WithTimeout(HTTP_BATCH_TIMEOUT)
      .PipeAsync(
        maxParallelism ?? MAX_PARALLELISM_HTTP,
        async x => await Download(x).ConfigureAwait(false),
        -1,
        false,
        _cts.Token
      )
      .Join()
      .Batch(MAX_SAVE_CACHE_BATCH)
      .WithTimeout(HTTP_BATCH_TIMEOUT)
      .ReadAllConcurrently(maxParallelism ?? MAX_SAVE_CACHE_PARALLELISM, SaveToCache, _cts.Token)
      .ContinueWith(
        t =>
        {
          Exception? ex = t.Exception;
          if (ex is null && t.Status is TaskStatus.Canceled && !_cts.Token.IsCancellationRequested)
          {
            ex = new OperationCanceledException();
          }

          if (ex is not null)
          {
            RecordException(ex);
          }

          _channel.Writer.TryComplete(ex);
        },
        _cts.Token,
        TaskContinuationOptions.ExecuteSynchronously,
        TaskScheduler.Current
      )
      .ConfigureAwait(false);

  public abstract string? CheckCache(string id);

  public async Task<List<T>> Download(List<string?> ids)
  {
    try
    {
      return await DownloadInternal(ids).ConfigureAwait(false);
    }
#pragma warning disable CA1031
    catch (Exception ex)
#pragma warning restore CA1031
    {
      RecordException(ex);
      return [];
    }
  }

  protected abstract Task<List<T>> DownloadInternal(List<string?> batch);

  public void SaveToCache(List<T> batch)
  {
    try
    {
      SaveToCacheInternal(batch);
    }
#pragma warning disable CA1031
    catch (Exception ex)
#pragma warning restore CA1031
    {
      RecordException(ex);
    }
  }

  protected abstract void SaveToCacheInternal(List<T> batch);

  protected Exception? Exception { get; private set; }

  private void RecordException(Exception ex)
  {
    Exception = ex;
    _channel.Writer.TryComplete(ex);
    //cancel everything!
    if (!_cts.IsCancellationRequested)
    {
      _cts.Cancel();
    }
  }
}
