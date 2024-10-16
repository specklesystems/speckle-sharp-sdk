using Speckle.Sdk.Common;
using Speckle.Sdk.Helpers;
using Speckle.Sdk.Logging;
using Speckle.Sdk.Serialisation.Utilities;
using Speckle.Sdk.Transports;
using Speckle.Sdk.Transports.ServerUtils;

namespace Speckle.Sdk.Serialisation;

public sealed class ObjectLoader(ISpeckleHttp http,
  ISdkActivityFactory activityFactory,
  Uri ServerUrl,
  string StreamId,
  string? token,
  string objectId,
   IProgress<ProgressArgs>? progress,
     SQLiteTransport transport) : IDisposable
{
  private const int HTTP_ID_CHUNK_SIZE = 50;
  private const int CACHE_CHUNK_SIZE = 50;
  private readonly ServerApi _api = new(http, activityFactory, ServerUrl, token, string.Empty);



  private async Task<string> GetRootJson()
  {
    var rootJson = await transport.GetObject(objectId).ConfigureAwait(false);
    if (rootJson == null)
    {
      rootJson = await _api.DownloadSingleObject(StreamId, objectId, progress).NotNull().ConfigureAwait(false);
      transport.SaveObjectSync(objectId, rootJson);
    }

    return rootJson;
  }

  public async Task Save(CancellationToken cancellationToken)
  {
    var rootJson = await GetRootJson().ConfigureAwait(false);
    var childrenIds = ClosureParser.GetChildrenIds(rootJson)
      .Where(x => !x.StartsWith("blob", StringComparison.Ordinal))
      .Batch(HTTP_ID_CHUNK_SIZE); //review this after putting in sqlite cache


    var downloads = childrenIds.Select(b => _api.DownloadObjectsImpl2(StreamId, b, progress));

    var toCache = new List<(string, string)>();
    var tasks = new List<Task>();
    await foreach (var (id, json) in downloads.SelectManyAsync().WithCancellation(cancellationToken))
    {
      toCache.Add((id, json));
      if (toCache.Count >= CACHE_CHUNK_SIZE)
      {
        Console.WriteLine("Caching objects " + toCache.Count);
        var toSave = toCache;
        toCache = new List<(string, string)>();
        tasks.Add(transport.SaveObjects(toSave));
      }
    }

    if (toCache.Count > 0)
    {
      tasks.Add(transport.SaveObjects(toCache));
      Console.WriteLine("Final cache " + toCache.Count);
    }
    await Task.WhenAll(tasks).ConfigureAwait(false);
  }

  public void Dispose()
  {
    _api.Dispose();
  }
}
