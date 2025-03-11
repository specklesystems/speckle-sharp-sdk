using System.Collections.Concurrent;
using Speckle.Sdk.Serialisation.V2;
using Speckle.Sdk.Serialisation.V2.Send;
using Speckle.Sdk.Testing.Framework;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Serialization.Tests;

public sealed class CancellationSqLiteJsonCacheManager(CancellationTokenSource cancellationTokenSource)
  : DummySqLiteJsonCacheManager
{
  public override void SaveObjects(IEnumerable<(string id, string json)> items)
  {
    cancellationTokenSource.Cancel();
    cancellationTokenSource.Token.ThrowIfCancellationRequested();
  }
}

public class CancellationSqLiteSendManager(CancellationTokenSource cancellationTokenSource) : DummySqLiteSendManager
{
  public override void SaveObjects(IEnumerable<(string id, string json)> items)
  {
    cancellationTokenSource.Cancel();
    cancellationTokenSource.Token.ThrowIfCancellationRequested();
  }
}

public class CancellationServerObjectManager(CancellationTokenSource cancellationTokenSource)
  : MemoryServerObjectManager(new ConcurrentDictionary<string, string>())
{
  public override Task UploadObjects(
    IReadOnlyList<BaseItem> objects,
    bool compressPayloads,
    IProgress<ProgressArgs>? progress,
    CancellationToken cancellationToken
  )
  {
    cancellationTokenSource.Cancel();
    cancellationTokenSource.Token.ThrowIfCancellationRequested();
    return base.UploadObjects(objects, compressPayloads, progress, cancellationToken);
  }

  public override Task<string?> DownloadSingleObject(
    string objectId,
    IProgress<ProgressArgs>? progress,
    CancellationToken cancellationToken
  )
  {
    cancellationTokenSource.Cancel();
    cancellationTokenSource.Token.ThrowIfCancellationRequested();
    return base.DownloadSingleObject(objectId, progress, cancellationToken);
  }
}
