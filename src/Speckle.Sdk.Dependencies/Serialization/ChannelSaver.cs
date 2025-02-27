using System.Buffers;
using System.Collections.Concurrent;
using System.Threading.Channels;
using Open.ChannelExtensions;
using Speckle.Sdk.Serialisation.V2.Send;

namespace Speckle.Sdk.Dependencies.Serialization;

public abstract class ChannelSaver<T>(CancellationToken cancellationToken)
  where T : IHasByteSize
{
  private const int SEND_CAPACITY = 500;
  private const int HTTP_SEND_CHUNK_SIZE = 25_000_000; //bytes
  private static readonly TimeSpan HTTP_BATCH_TIMEOUT = TimeSpan.FromSeconds(2);
  private const int MAX_PARALLELISM_HTTP = 4;
  private const int HTTP_CAPACITY = 500;
  private const int MAX_CACHE_WRITE_PARALLELISM = 4;
  private const int MAX_CACHE_BATCH = 500;

  private readonly ConcurrentBag<Exception> _exceptions = new();
  private readonly CancellationTokenSource _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
  
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

  public Task Start() =>
    _checkCacheChannel
      .Reader.BatchByByteSize(HTTP_SEND_CHUNK_SIZE)
      .WithTimeout(HTTP_BATCH_TIMEOUT)
      .PipeAsync(
        MAX_PARALLELISM_HTTP,
        async x => await SendToServer(x).ConfigureAwait(false),
        HTTP_CAPACITY,
        false,
        _cts.Token
      )
      .Join()
      .Batch(MAX_CACHE_BATCH)
      .WithTimeout(HTTP_BATCH_TIMEOUT)
      .ReadAllConcurrently(MAX_CACHE_WRITE_PARALLELISM, SaveToCache, _cts.Token)
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
          _checkCacheChannel.Writer.TryComplete(ex);
        },
        _cts.Token,
        TaskContinuationOptions.ExecuteSynchronously,
        TaskScheduler.Current
      );

  public async ValueTask<bool> TrySave(T item)
  {
    if (IsCompleted)
    {
      return false;
    }
    await _checkCacheChannel.Writer.WriteAsync(item).ConfigureAwait(false);
    return true;
  }

  public bool IsCompleted => !_exceptions.IsEmpty;

  private async Task<IMemoryOwner<T>> SendToServer(IMemoryOwner<T> batch)
  {
    try
    {
      await SendToServer((Batch<T>)batch).ConfigureAwait(false);
      return batch;
    }
#pragma warning disable CA1031
    catch (Exception ex)
#pragma warning restore CA1031
    {
      RecordException(ex);
      return batch;
    }
  }

  public async Task SendToServer(Batch<T> batch)
  {
    try
    {
      await SendToServerInternal(batch).ConfigureAwait(false);
    }
#pragma warning disable CA1031
    catch (Exception ex)
#pragma warning restore CA1031
    {
      RecordException(ex);
    }
  }

  protected abstract Task SendToServerInternal(Batch<T> batch);

  public abstract void SaveToCache(List<T> item);

  public Task DoneTraversing()
  {
    _checkCacheChannel.Writer.TryComplete();
    return Task.CompletedTask;
  }

  public async Task DoneSaving()
  {
    if (!_checkCacheChannel.Reader.Completion.IsCompleted)
    {
      await _checkCacheChannel.Reader.Completion.ConfigureAwait(false);
    }
    ThrowIfFailed();
  }

  public void ThrowIfFailed()
  {
    if (!_exceptions.IsEmpty)
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

  private void RecordException(Exception ex)
  {
    if (ex is AggregateException ae)
    {
      foreach (var innerException in ae.Flatten().InnerExceptions)
      {
        _exceptions.Add(innerException);
      }
    }
    else
    {
      _exceptions.Add(ex);
    }
    _checkCacheChannel.Writer.TryComplete(ex);
    _cts.Cancel();
  }
}
