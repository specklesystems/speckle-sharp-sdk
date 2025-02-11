using System.Buffers;
using System.Threading.Channels;
using Open.ChannelExtensions;
using Speckle.Sdk.Serialisation.V2.Send;

namespace Speckle.Sdk.Dependencies.Serialization;

public abstract class ChannelSaver<T>
  where T : IHasSize, new()
{
  private readonly T EMPTY = new T();
  private bool _emptied;
  private const int SEND_CAPACITY = 500;
  private const int HTTP_SEND_CHUNK_SIZE = 25_000_000; //bytes
  private static readonly TimeSpan HTTP_BATCH_TIMEOUT = TimeSpan.FromSeconds(2);
  private const int MAX_PARALLELISM_HTTP = 4;
  private const int HTTP_CAPACITY = 500;
  private const int MAX_CACHE_WRITE_PARALLELISM = 4;
  private const int MAX_CACHE_BATCH = 500;

  private readonly List<Exception> _exceptions = new();
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

  public Task Start(CancellationToken cancellationToken) =>
    _checkCacheChannel
      .Reader.BatchBySize(HTTP_SEND_CHUNK_SIZE)
      .WithTimeout(HTTP_BATCH_TIMEOUT)
      .PipeAsync(
        MAX_PARALLELISM_HTTP,
        async x => await SendToServer(x).ConfigureAwait(false),
        HTTP_CAPACITY,
        false,
        cancellationToken
      )
      .Join()
      .Batch(MAX_CACHE_BATCH)
      .WithTimeout(HTTP_BATCH_TIMEOUT)
      .ReadAllConcurrently(MAX_CACHE_WRITE_PARALLELISM, SaveToCacheInternal, cancellationToken)
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
            lock (_exceptions)
            {
              _exceptions.Add(ex);
            }
          }
          _checkCacheChannel.Writer.TryComplete(ex);
        },
        CancellationToken.None,
        TaskContinuationOptions.ExecuteSynchronously,
        TaskScheduler.Current
      );

  public async ValueTask Save(T item, CancellationToken cancellationToken) =>
    await _checkCacheChannel.Writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);

  private async Task<IMemoryOwner<T>> SendToServer(IMemoryOwner<T> batch)
  {
    var b = (Batch<T>)batch;
    int index = b.Items.IndexOf(EMPTY);
    if (index != -1)
    {
      b.Items.RemoveAt(index);
    }
    await SendToServer((Batch<T>)batch).ConfigureAwait(false);
    if (index != -1)
    {
      b.Add(EMPTY);
    }
    return batch;
  }

  public abstract Task SendToServer(Batch<T> batch);

  private void SaveToCacheInternal(List<T> batch)
  {
    int index = batch.IndexOf(EMPTY);
    if (index != -1)
    {
      batch.RemoveAt(index);
    }
    SaveToCache(batch);
    if (index != -1)
    {
      _emptied = true;
    }
  }

  public abstract void SaveToCache(List<T> item);

  public async Task DoneTraversing()
  {
    await Save(EMPTY, CancellationToken.None).ConfigureAwait(false);
    _checkCacheChannel.Writer.TryComplete();
  }

  public async Task DoneSaving(CancellationToken cancellationToken)
  {
    await _checkCacheChannel.Reader.Completion.ConfigureAwait(false);
    while (!_emptied && !cancellationToken.IsCancellationRequested && _exceptions.Count == 0)
    {
      await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
    }
    lock (_exceptions)
    {
      if (_exceptions.Count > 0)
      {
        var exceptions = new List<Exception>();
        foreach (var ex in _exceptions)
        {
          if (ex is AggregateException ae)
          {
            exceptions.AddRange(ae.Flatten().InnerExceptions);
          }
          else
          {
            exceptions.Add(ex);
          }
        }
        throw new AggregateException(exceptions);
      }
    }
  }
}
