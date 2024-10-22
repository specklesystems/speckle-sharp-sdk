using System.Runtime.CompilerServices;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Serialisation.V2.Send;

public class ObjectSender(
  ISQLiteCacheManager sqLiteCacheManager,
  IServerObjectManager serverObjectManager,
  string streamId,
  IProgress<ProgressArgs>? progress)
{
  private const int BATCH_SIZE_HAS_OBJECTS = 100000;
  private const int MAX_REQUEST_SIZE = 100_000_000;
  
  public async IAsyncEnumerable<string> HasObjects(
    IAsyncEnumerable<string> ids,
    [EnumeratorCancellation] CancellationToken cancellationToken
  )
  {
    await foreach (var idBatch in ids.BatchAsync(BATCH_SIZE_HAS_OBJECTS).WithCancellation(cancellationToken))
    {
      var hasResults = await serverObjectManager.HasObjects(streamId, idBatch, cancellationToken).ConfigureAwait(false);
      foreach (var kvp in hasResults.Where(kvp => kvp.Value))
      {
        yield return kvp.Key;
      }
    }
  }
  
  public async Task CacheAndSend(
    IAsyncEnumerable<(string, string)> objects,
    [EnumeratorCancellation] CancellationToken cancellationToken
  )
  {
    await foreach (var (id, json) in objects.WithCancellation(cancellationToken))
    {
      await foreach(var (id, exists) in sqLiteCacheManager.HasObjects2(batch.Select(x => x.Item1)).WithCancellation(cancellationToken))
      {
        
      }
    }
  }
  
   
  private async IAsyncEnumerable<(string, string)> Cache(
    IAsyncEnumerable<(string, string)> objects,
    [EnumeratorCancellation] CancellationToken cancellationToken
  )
  {
    var x = await objects.BatchAsync(100).ToListAsync(cancellationToken).ConfigureAwait(false);
    await foreach (var batch in objects.BatchAsync(100).WithCancellation(cancellationToken))
    {
      await foreach(var (id, exists) in sqLiteCacheManager.HasObjects2(batch.Select(x => x.Item1)).WithCancellation(cancellationToken))
      {
        sqLiteCacheManager.SaveObjects();
      }
    }
  }
}
