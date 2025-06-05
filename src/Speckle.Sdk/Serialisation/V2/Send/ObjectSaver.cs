using Microsoft.Extensions.Logging;
using Speckle.Sdk.Dependencies;
using Speckle.Sdk.Dependencies.Serialization;
using Speckle.Sdk.SQLite;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Serialisation.V2.Send;

public interface IObjectSaver : IDisposable
{
  Exception? Exception { get; set; }
  Task Start(int? maxParallelism, int? httpBatchSize, int? cacheBatchSize, CancellationToken cancellationToken);
  void DoneTraversing();
  Task DoneSaving();
  Task SaveAsync(BaseItem item);
}

public sealed class ObjectSaver(
  IProgress<ProgressArgs>? progress,
  ISqLiteJsonCacheManager sqLiteJsonCacheManager,
  IServerObjectManager serverObjectManager,
  ILogger<ObjectSaver> logger,
  CancellationToken cancellationToken,
#pragma warning disable CS9107
#pragma warning disable CA2254
  SerializeProcessOptions? options = null
) : ChannelSaver<BaseItem>, IObjectSaver
#pragma warning restore CA2254
#pragma warning restore CS9107
{
  private readonly CancellationTokenSource _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
    cancellationToken
  );

  private readonly SerializeProcessOptions _options = options ?? new();

  private long _uploading;
  private long _cached;

  private long _objectsSerialized;

  protected override async Task SendToServerInternal(Batch<BaseItem> batch)
  {
    if (_cancellationTokenSource.IsCancellationRequested)
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
      _cancellationTokenSource.Cancel();
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
    if (_cancellationTokenSource.IsCancellationRequested)
    {
      return;
    }
    try
    {
      if (!_options.SkipCacheWrite && batch.Count != 0)
      {
        sqLiteJsonCacheManager.SaveObjects(batch.Select(x => (x.Id.Value, x.Json.Value)));
        Interlocked.Add(ref _cached, batch.Count);
        progress?.Report(new(ProgressEvent.CachedToLocal, _cached, _objectsSerialized));
      }
    }
    catch (OperationCanceledException)
    {
      _cancellationTokenSource.Cancel();
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

  private void RecordException(Exception e)
  {
    //order here matters
    logger.LogError(e, "Error in SDK: {message}", e.Message);
    Exception = e;
    _cancellationTokenSource.Cancel();
  }

  public void Dispose()
  {
    _cancellationTokenSource.Dispose();
    sqLiteJsonCacheManager.Dispose();
  }
}
