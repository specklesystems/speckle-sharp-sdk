using Speckle.Sdk.Common;
using Speckle.Sdk.Helpers;
using Speckle.Sdk.Logging;
using Speckle.Sdk.Transports.ServerUtils;

namespace Speckle.Sdk.Transports;

public interface IModelSource : IDisposable
{
  ValueTask<string> GetJson(string objectId, Action<ProgressArgs> progress);

  IAsyncEnumerable<(string, string)> GetJsons(IReadOnlyList<string> objectIds, Action<ProgressArgs> progress);
}

public sealed class ServerSource(
  ISpeckleHttp speckleHttp,
  ISdkActivityFactory activityFactory,
  Uri baseUri,
  string streamId,
  string? authorizationToken
) : IModelSource
{
  private readonly ServerApi _serverApi = new(speckleHttp, activityFactory, baseUri, authorizationToken, string.Empty);

  public async ValueTask<string> GetJson(string objectId, Action<ProgressArgs> progress)
  {
    var json = await _serverApi.DownloadSingleObject(streamId, objectId, progress).ConfigureAwait(false);
    return json.NotNull();
  }

  public async IAsyncEnumerable<(string, string)> GetJsons(
    IReadOnlyList<string> objectIds,
    Action<ProgressArgs> progress
  )
  {
    await foreach (var (id, json) in _serverApi.DownloadObjects2(streamId, objectIds, progress))
    {
      yield return new(id, json);
    }
  }

  public void Dispose() => _serverApi.Dispose();
}

public sealed class MemorySource(IReadOnlyDictionary<string, string> idToJson) : IModelSource
{
  public void Dispose() { }

  public ValueTask<string> GetJson(string objectId, Action<ProgressArgs> progress) =>
    new(Task.FromResult(idToJson[objectId].NotNull()));

  public async IAsyncEnumerable<(string, string)> GetJsons(
    IReadOnlyList<string> objectIds,
    Action<ProgressArgs> progress
  )
  {
    foreach (var id in objectIds)
    {
      yield return (id, idToJson[id].NotNull());
    }

    await Task.CompletedTask.ConfigureAwait(false);
  }
}
