using Microsoft.Extensions.Logging;
using Speckle.Sdk.Common;
using Speckle.Sdk.Dependencies;
using Speckle.Sdk.Dependencies.Serialization;
using Speckle.Sdk.SQLite;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Serialisation.V2.Send;

public interface IObjectSaver : IDisposable
{
  Exception? Exception { get; set; }
  Task Start(
    int? maxParallelism,
    int? httpBatchSize,
    int? blobBatchSize,
    int? cacheBatchSize,
    CancellationToken cancellationToken
  );
  void DoneTraversing();
  Task DoneSaving();
  Task SaveAsync(BaseItem item);
}

public sealed class ObjectSaver(
  IProgress<ProgressArgs>? progress,
  ISqLiteJsonCacheManager sqLiteJsonCacheManager,
  IServerObjectManager serverObjectManager,
  IServerBlobManager? serverBlobManager,
  ILogger<ObjectSaver> logger,
  SerializeProcessOptions options,
  CancellationToken cancellationToken
) : ChannelSaver<BaseItem, BlobItem>, IObjectSaver
{
  private readonly CancellationTokenSource _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
    cancellationToken
  );

  private readonly SerializeProcessOptions _options = options ?? new();

  private long _uploading;
  private long _cached;

  private long _objectsSerialized;
  private bool _disposed;

  protected override async Task SendBlobToServerInternal(Batch<BlobItem> batch)
  {
    // Callers should either setup a blob manager, or not try and send blobs
    serverBlobManager.NotNull("No blob manager was setup to handle sending blobs");

    var objectBatch = batch.Items.Distinct().Select(x => (x.Blob.id.NotNull(), x.Blob.filePath)).ToList();
    // var hasObjects = await serverBlobManager
    //   .HasObjects(objectBatch.Select(x => x.Id.Value).Freeze(), _cancellationTokenSource.Token)
    //   .ConfigureAwait(false);
    // objectBatch = batch.Items.Where(x => !hasObjects[x.Id.Value]).ToList();
    if (objectBatch.Count != 0)
    {
      // Interlocked.Add(ref _uploading, batch.Items.Count);
      // progress?.Report(new(ProgressEvent.UploadingObjects, _uploading, null));
      await serverBlobManager.UploadBlobs(objectBatch, progress, _cancellationTokenSource.Token).ConfigureAwait(false);
    }
  }

  protected override async Task SendToServerInternal(Batch<BaseItem> batch)
  {
    if (IsCancelled())
    {
      return;
    }
    try
    {
      if (!_options.SkipServer && batch.Items.Count != 0)
      {
        var objectBatch = batch.Items.Distinct().ToList();
        var hasObjects = await serverObjectManager
          .HasObjects(objectBatch.Select(x => x.Id.Value).Freeze(), _cancellationTokenSource.Token)
          .ConfigureAwait(false);
        objectBatch = batch.Items.Where(x => !hasObjects[x.Id.Value]).ToList();
        if (objectBatch.Count != 0)
        {
          Interlocked.Add(ref _uploading, batch.Items.Count);
          progress?.Report(new(ProgressEvent.UploadingObjects, _uploading, null));
          await serverObjectManager
            .UploadObjects(objectBatch, true, progress, _cancellationTokenSource.Token)
            .ConfigureAwait(false);
        }
      }
    }
    catch (OperationCanceledException)
    {
      CancelSaving();
    }
#pragma warning disable CA1031
    catch (Exception e)
#pragma warning restore CA1031
    {
      RecordException(e);
      logger.LogError(
        "Error while sending objects to server, some stats of the payload: {Count} objects, {Serialized} serialized, {Cached} cached, {BatchByteSize} batch bytes",
        batch.Items.Count,
        _objectsSerialized,
        _cached,
        batch.BatchByteSize
      );
    }
  }

  public async Task SaveAsync(BaseItem item)
  {
    Interlocked.Increment(ref _objectsSerialized);
    await SaveAsync(item, _cancellationTokenSource.Token).ConfigureAwait(false);
  }

  public override void SaveToCache(List<BaseItem> batch)
  {
    if (IsCancelled())
    {
      return;
    }
    try
    {
      if (!_options.SkipCacheWrite && batch.Count != 0)
      {
        Interlocked.Add(ref _cached, batch.Count);
        progress?.Report(new(ProgressEvent.CachedToLocal, _cached, _objectsSerialized));
        sqLiteJsonCacheManager.SaveObjects(batch.Select(x => (x.Id.Value, x.Json.Value)));
      }
    }
    catch (OperationCanceledException)
    {
      CancelSaving();
    }
#pragma warning disable CA1031
    catch (Exception e)
#pragma warning restore CA1031
    {
      RecordException(e);
      logger.LogError(
        "Error while saving to cache, some stats of the payload: {Count} objects, {Serialized} serialized, {Cached} cached, {BatchByteSize} batch bytes",
        batch.Count,
        _objectsSerialized,
        _cached,
        batch.Sum(x => x.ByteSize)
      );
    }
  }

  private bool IsCancelled() => _disposed || _cancellationTokenSource.IsCancellationRequested;

  private void CancelSaving()
  {
    if (IsCancelled())
    {
      return;
    }
    _cancellationTokenSource.Cancel();
  }

  private void RecordException(Exception e)
  {
    if (IsCancelled())
    {
      return;
    }
    //order here matters
    logger.LogError(e, "Error in SDK: {message}", e.Message);
    Exception = e;
    _cancellationTokenSource.Cancel();
  }

  public void Dispose()
  {
    _disposed = true;
    _cancellationTokenSource.Dispose();
    sqLiteJsonCacheManager.Dispose();
  }
}
