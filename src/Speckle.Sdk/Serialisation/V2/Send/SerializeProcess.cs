using Speckle.Sdk.Dependencies.Serialization;
using Speckle.Sdk.Models;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Serialisation.V2.Send;

public record SerializeProcessOptions(bool SkipCache, bool SkipServer);

public class SerializeProcess(
  IProgress<ProgressArgs>? progress,
  ISQLiteSendCacheManager sqliteSendCacheManager,
  IServerObjectManager serverObjectManager
) : ChannelSaver
{
  private long _uploaded;
  private long _cached;

  private SerializeProcessOptions _options = new(false, false);

  public async Task<(string rootObjId, IReadOnlyDictionary<string, ObjectReference> convertedReferences)> Serialize(
    string streamId,
    Base root,
    CancellationToken cancellationToken,
    SerializeProcessOptions? options = null
  )
  {
    _options = options ?? _options;
    var channelTask = Start(streamId, cancellationToken);
    var serializer2 = new SpeckleObjectSerializer2(this, progress, true, cancellationToken);
    await serializer2.Serialize(root).ConfigureAwait(false);
    await Done().ConfigureAwait(false);
    await channelTask.ConfigureAwait(false);
    return (root.id, serializer2.ObjectReferences);
  }

  public override async Task<List<BaseItem>> SendToServer(
    string streamId,
    List<BaseItem> batch,
    CancellationToken cancellationToken
  )
  {
    if (batch.Count == 0)
    {
      progress?.Report(new(ProgressEvent.UploadedObjects, _uploaded, null));
      return batch;
    }

    if (!_options.SkipServer)
    {
      await serverObjectManager.UploadObjects(streamId, batch, true, progress, cancellationToken).ConfigureAwait(false);
      Interlocked.Exchange(ref _uploaded, _uploaded + batch.Count);
      progress?.Report(new(ProgressEvent.UploadedObjects, _uploaded, null));
    }
    return batch;
  }

  public override void SaveToCache(List<BaseItem> items)
  {
    if (!_options.SkipCache)
    {
      if (items.Count == 0)
      {
        progress?.Report(new(ProgressEvent.CachedToLocal, _cached, null));
        return;
      }
      sqliteSendCacheManager.SaveObjects(items);
      Interlocked.Exchange(ref _cached, _cached + items.Count);
      progress?.Report(new(ProgressEvent.CachedToLocal, _cached, null));
    }
  }
}
