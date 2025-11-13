using System.Buffers;
using Open.ChannelExtensions;
using Speckle.Sdk.Serialisation.V2.Send;

namespace Speckle.Sdk.Dependencies.Serialization;

public abstract class ChannelSaver<TItem, TBlobItem>
  where TItem : IHasByteSize
  where TBlobItem : IHasByteSize, TItem
{
  private const int HTTP_SEND_CHUNK_SIZE = 25_000_000; //bytes
  private const int BLOB_SEND_CHUNK_SIZE = 10; //count
  private static readonly TimeSpan HTTP_BATCH_TIMEOUT = TimeSpan.FromSeconds(2);
  private const int MAX_PARALLELISM_HTTP = 4;
  private const int MAX_CACHE_BATCH = 1000;

  private readonly BroadcastChannel<TItem> _broadcastChannel = new();

  public async Task Start(
    int? maxParallelism,
    int? httpBatchSize,
    int? blobSendCache,
    int? cacheBatchSize,
    CancellationToken cancellationToken
  )
  {
    maxParallelism ??= MAX_PARALLELISM_HTTP;
    httpBatchSize ??= HTTP_SEND_CHUNK_SIZE;
    blobSendCache ??= BLOB_SEND_CHUNK_SIZE;
    cacheBatchSize ??= MAX_CACHE_BATCH;
    await StartInternal(
        maxParallelism.Value,
        httpBatchSize.Value,
        blobSendCache.Value,
        cacheBatchSize.Value,
        cancellationToken
      )
      .ConfigureAwait(false);
  }

  private Task StartInternal(
    int maxParallelism,
    int httpBatchSize,
    int blobSendCache,
    int cacheBatchSize,
    CancellationToken cancellationToken
  )
  {
    Task serverSend = _broadcastChannel
      .Subscribe()
      .BatchByByteSize(httpBatchSize)
      .WithTimeout(HTTP_BATCH_TIMEOUT)
      .ReadAllConcurrentlyAsync(
        maxParallelism,
        async x => await SendToServer(x).ConfigureAwait(false),
        cancellationToken
      );

    Task writeCache = _broadcastChannel
      .Subscribe()
      .Batch(cacheBatchSize)
      .ReadAll(SaveToCache, true, cancellationToken: cancellationToken)
      .AsTask();

    Task blobsCache = _broadcastChannel
      .Subscribe()
      .OfType<TItem, TBlobItem>()
      .BatchByByteSize(blobSendCache)
      .ReadAllAsync(
        async x => await SendBlobToServer(x).ConfigureAwait(false),
        true,
        cancellationToken: cancellationToken
      )
      .AsTask();

    return Task.WhenAll(serverSend, writeCache, blobsCache);

    // return _broadcastChannel
    //   .Subscribe()
    //   .BatchByByteSize(httpBatchSize ?? HTTP_SEND_CHUNK_SIZE)
    //   .WithTimeout(HTTP_BATCH_TIMEOUT)
    //   .PipeAsync(
    //     maxParallelism ?? MAX_PARALLELISM_HTTP,
    //     async x => await SendToServer(x).ConfigureAwait(false),
    //     HTTP_CAPACITY,
    //     false,
    //     cancellationToken
    //   )
    //   .Join()
    //   .Batch(cacheBatchSize ?? MAX_CACHE_BATCH, singleReader: true)
    //   .WithTimeout(HTTP_BATCH_TIMEOUT)
    //   .ReadAllConcurrently(MAX_CACHE_WRITE_PARALLELISM, SaveToCache, cancellationToken)
    //   .ContinueWith(
    //     t =>
    //     {
    //       Exception? ex = t.Exception;
    //       if (ex is null && t.Status is TaskStatus.Canceled && !cancellationToken.IsCancellationRequested)
    //       {
    //         ex = new OperationCanceledException();
    //       }
    //
    //       if (ex is not null)
    //       {
    //         RecordException(ex);
    //       }
    //
    //       _checkCacheChannel.Writer.TryComplete(ex);
    //     },
    //     cancellationToken,
    //     TaskContinuationOptions.ExecuteSynchronously,
    //     TaskScheduler.Current
    //   );
  }

  private async ValueTask SendBlobToServer(IMemoryOwner<TBlobItem> batch)
  {
    try
    {
      await SendBlobToServerInternal((Batch<TBlobItem>)batch).ConfigureAwait(false);
    }
#pragma warning disable CA1031
    catch (Exception ex)
#pragma warning restore CA1031
    {
      RecordException(ex);
    }
  }

  protected abstract Task SendBlobToServerInternal(Batch<TBlobItem> batch);

  public async Task SaveAsync(TItem item, CancellationToken cancellationToken)
  {
    if (Exception is not null)
    {
      return; //don't save if we're already done through an error
    }
    //can switch to check then try pattern when back pressure is needed or exceptions are too much
    //the trees don't need to respond to back pressure
    await _broadcastChannel.WriteAsync(item, cancellationToken).ConfigureAwait(false);
  }

  private async Task SendToServer(IMemoryOwner<TItem> batch)
  {
    try
    {
      await SendToServerInternal((Batch<TItem>)batch).ConfigureAwait(false);
    }
#pragma warning disable CA1031
    catch (Exception ex)
#pragma warning restore CA1031
    {
      RecordException(ex);
    }
  }

  protected abstract Task SendToServerInternal(Batch<TItem> batch);

  public abstract void SaveToCache(List<TItem> item);

  public void DoneTraversing() => _broadcastChannel.CompleteWriters();

  public async Task DoneSaving()
  {
    if (!_broadcastChannel.IsReadingCompleted())
    {
      await _broadcastChannel.CompleteReaders().ConfigureAwait(false);
    }
  }

  public Exception? Exception { get; set; }

  private void RecordException(Exception ex)
  {
    Exception = ex;
  }
}
