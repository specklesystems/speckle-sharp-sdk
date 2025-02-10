using System.Threading.Channels;
using Open.ChannelExtensions;

namespace Speckle.Sdk.Dependencies.Serialization;

public abstract class ChannelLoader<T>
{
  private const int RECEIVE_CAPACITY = 5000;

  private const int HTTP_GET_CHUNK_SIZE = 500;
  private const int MAX_PARALLELISM_HTTP = 4;
  private static readonly TimeSpan HTTP_BATCH_TIMEOUT = TimeSpan.FromSeconds(2);
  private const int MAX_SAVE_CACHE_BATCH = 500;
  private const int MAX_SAVE_CACHE_PARALLELISM = 4;

  private readonly List<Exception> _exceptions = new();
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

  protected async Task GetAndCache(
    IEnumerable<string> allChildrenIds,
    CancellationToken cancellationToken,
    int? maxParallelism = null
  ) =>
    await _channel
      .Source(allChildrenIds, cancellationToken)
      .Pipe(maxParallelism ?? Environment.ProcessorCount, CheckCache, cancellationToken: cancellationToken)
      .Filter(x => x is not null)
      .Batch(HTTP_GET_CHUNK_SIZE)
      .WithTimeout(HTTP_BATCH_TIMEOUT)
      .PipeAsync(
        maxParallelism ?? MAX_PARALLELISM_HTTP,
        async x => await Download(x).ConfigureAwait(false),
        -1,
        false,
        cancellationToken
      )
      .Join()
      .Batch(MAX_SAVE_CACHE_BATCH)
      .WithTimeout(HTTP_BATCH_TIMEOUT)
      .ReadAllConcurrently(maxParallelism ?? MAX_SAVE_CACHE_PARALLELISM, SaveToCache, cancellationToken)
      .ContinueWith(
        t =>
        {
          Exception? ex = t.Exception;
          if (ex is null && t.Status is TaskStatus.Canceled && !cancellationToken.IsCancellationRequested)
          {
            ex = new OperationCanceledException();
          }

          if (ex is not null)
          {
            if (ex is AggregateException ae)
            {
              _exceptions.AddRange(ae.Flatten().InnerExceptions);
            }
            else
            {
              _exceptions.Add(ex);
            }
          }

          _channel.Writer.TryComplete(ex);
        },
        CancellationToken.None,
        TaskContinuationOptions.ExecuteSynchronously,
        TaskScheduler.Current
      )
      .ConfigureAwait(false);

  public void CheckForExceptions()
  {
    if (_exceptions.Count > 0)
    {
      throw new AggregateException(_exceptions);
    }
  }

  public abstract string? CheckCache(string id);

  public abstract Task<List<T>> Download(List<string?> ids);

  public abstract void SaveToCache(List<T> x);
}
