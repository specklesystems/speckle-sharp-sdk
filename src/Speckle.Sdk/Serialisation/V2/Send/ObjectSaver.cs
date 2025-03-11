using Microsoft.Extensions.Logging;
using Speckle.Sdk.Dependencies;
using Speckle.Sdk.Dependencies.Serialization;
using Speckle.Sdk.SQLite;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Serialisation.V2.Send;

public interface IObjectSaver : IDisposable
{
  Exception? Exception { get; }
  Task Start();
  void DoneTraversing();
  Task DoneSaving();
  ValueTask SaveItem(BaseItem item);
}
public sealed class ObjectSaver(
  IProgress<ProgressArgs>? progress,
  ISqLiteJsonCacheManager sqLiteJsonCacheManager,
  IServerObjectManager serverObjectManager,
  ILoggerFactory loggerFactory,
  CancellationToken cancellationToken,
 
#pragma warning disable CS9107
#pragma warning disable CA2254
  SerializeProcessOptions? options = null)  : ChannelSaver<BaseItem>(x => loggerFactory.CreateLogger<SerializeProcess>().LogWarning(x), cancellationToken), IObjectSaver

#pragma warning restore CA2254
#pragma warning restore CS9107
{
  private readonly SerializeProcessOptions _options = options ?? new();
  private readonly ILogger<SerializeProcess> _logger = loggerFactory.CreateLogger<SerializeProcess>();

  private long _uploaded;
  private long _cached;

  private long _objectsSerialized;
  protected override async Task SendToServerInternal(Batch<BaseItem> batch)
  {
    try
    {
      if (!_options.SkipServer && batch.Items.Count != 0)
      {
        var objectBatch = batch.Items.Distinct().ToList();
        var hasObjects = await serverObjectManager
          .HasObjects(objectBatch.Select(x => x.Id.Value).Freeze(), cancellationToken)
          .ConfigureAwait(false);
        objectBatch = batch.Items.Where(x => !hasObjects[x.Id.Value]).ToList();
        if (objectBatch.Count != 0)
        {
          await serverObjectManager.UploadObjects(objectBatch, true, progress, cancellationToken).ConfigureAwait(false);
          Interlocked.Exchange(ref _uploaded, _uploaded + batch.Items.Count);
        }

        progress?.Report(new(ProgressEvent.UploadedObjects, _uploaded, null));
      }
    }
    catch (OperationCanceledException)
    {
      throw;
    }
#pragma warning disable CA1031
    catch (Exception e)
#pragma warning restore CA1031
    {
      _logger.LogError(e, "Error sending objects to server");
      throw;
    }
  }

  public async ValueTask SaveItem(BaseItem item)
  {
    Interlocked.Increment(ref _objectsSerialized);
    
    await Save(item).ConfigureAwait(false);
  }

  public override void SaveToCache(List<BaseItem> batch)
  {
    try
    {
      if (!_options.SkipCacheWrite && batch.Count != 0)
      {
        sqLiteJsonCacheManager.SaveObjects(batch.Select(x => (x.Id.Value, x.Json.Value)));
        Interlocked.Exchange(ref _cached, _cached + batch.Count);
        progress?.Report(new(ProgressEvent.CachedToLocal, _cached, _objectsSerialized));
      }
    }
    catch (OperationCanceledException)
    {
      throw;
    }
#pragma warning disable CA1031
    catch (Exception e)
#pragma warning restore CA1031
    {
      _logger.LogError(e, "Error sending objects to server");
      throw;
    }
  }
  
  public void Dispose() => sqLiteJsonCacheManager.Dispose();
}
