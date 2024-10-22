using System.Threading.Channels;
using Open.ChannelExtensions;
using Speckle.Sdk.Common;
using Speckle.Sdk.Models;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Serialisation.V2.Send;

public class SerializeProcess
{
  private readonly IProgress<ProgressArgs>? _progress;
  private readonly ISQLiteCacheManager _sqliteCacheManager;
  private readonly IServerObjectManager _serverObjectManager;
  private readonly Channel<(string, string)> _checkCacheChannel;

  public SerializeProcess(IProgress<ProgressArgs>? progress, ISQLiteCacheManager sqliteCacheManager, IServerObjectManager serverObjectManager)
  {
    _progress = progress;
    _sqliteCacheManager = sqliteCacheManager;
    _serverObjectManager = serverObjectManager;
    _checkCacheChannel = Channel.CreateUnbounded<(string, string)>();
  }

  public async Task Serialize(string streamId,
    Base root,
    CancellationToken cancellationToken
  )
  {
    var task = _checkCacheChannel.Reader
      .Pipe(4, x => CheckCache(root.id, x), cancellationToken: cancellationToken)
      .Filter(x => x is not null)
      .Batch(100).WithTimeout(TimeSpan.FromSeconds(2))
      .ReadAllConcurrentlyAsync(4, async batch => await SendToServer(root.id, streamId, batch, cancellationToken).ConfigureAwait(false),
        cancellationToken)
      .ConfigureAwait(false);


    var serializer = new SpeckleObjectSerializer(async (id, json) =>
    {
      await _checkCacheChannel.Writer.WriteAsync((id, json), cancellationToken).ConfigureAwait(false);
    }, _progress, false, cancellationToken);
    var rootJson = await serializer.SerializeAsync(root).ConfigureAwait(true);
    await _checkCacheChannel.Writer.WriteAsync((root.id, rootJson), cancellationToken).ConfigureAwait(false);

    await task;
  }
  private (string, string)? CheckCache(string rootId, (string, string) item)
  {
      if (!_sqliteCacheManager.HasObject(item.Item1))
      {
        return item;
      }
      if (item.Item1 == rootId)
      {
        _checkCacheChannel.Writer.TryComplete();
      }
      return null;
  }
  
  private async Task SendToServer(string rootId, string streamId, IReadOnlyList<(string, string)?> batch, CancellationToken cancellationToken)
  {
    await _serverObjectManager.UploadObjects(streamId, batch.Select(x => x.NotNull()).ToList(), true, _progress, cancellationToken).ConfigureAwait(false);
    if (batch.Select(x => x.NotNull().Item1).Contains(rootId))
    {
      _checkCacheChannel.Writer.Complete();
    }
  }
}
