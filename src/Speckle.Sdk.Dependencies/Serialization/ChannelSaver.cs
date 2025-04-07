using System.Buffers;
using System.Threading.Channels;
using Open.ChannelExtensions;
using Speckle.Sdk.Serialisation.V2.Send;

namespace Speckle.Sdk.Dependencies.Serialization;

public abstract class ChannelSaver<T>
  where T : IHasByteSize
{
  private const int SEND_CAPACITY = 500;
  private const int HTTP_SEND_CHUNK_SIZE = 25_000_000; //bytes
  private static readonly TimeSpan HTTP_BATCH_TIMEOUT = TimeSpan.FromSeconds(2);
  private const int MAX_PARALLELISM_HTTP = 4;
  private const int HTTP_CAPACITY = 500;
  private const int MAX_CACHE_WRITE_PARALLELISM = 4;
  private const int MAX_CACHE_BATCH = 500;

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
      .Reader.BatchByByteSize(HTTP_SEND_CHUNK_SIZE)
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
      .ReadAllConcurrently(MAX_CACHE_WRITE_PARALLELISM, SaveToCache, cancellationToken)
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
            RecordException(ex);
          }
          _checkCacheChannel.Writer.TryComplete(ex);
        },
        cancellationToken,
        TaskContinuationOptions.ExecuteSynchronously,
        TaskScheduler.Current
      );

  public void Save(T item, CancellationToken cancellationToken)
  {
    if (Exception is not null || cancellationToken.IsCancellationRequested)
    {
      return; //don't save if we're already done through an error
    }
    // ReSharper disable once MethodSupportsCancellation
    _checkCacheChannel.Writer.TryWrite(item);
  }

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

  public void DoneTraversing() => _checkCacheChannel.Writer.TryComplete();

  public async Task DoneSaving()
  {
    if (!_checkCacheChannel.Reader.Completion.IsCompleted)
    {
      await _checkCacheChannel.Reader.Completion.ConfigureAwait(false);
    }
  }

  protected Exception? Exception { get; set; }

  private void RecordException(Exception ex)
  {
    Exception = ex;
    _checkCacheChannel.Writer.TryComplete(ex);
  }
}
