using Speckle.Sdk.Common;
using Speckle.Sdk.Transports;
using Speckle.Sdk.Transports.ServerUtils;

namespace Speckle.Sdk.Serialisation.Receive;

public record Downloaded(string Id, string Json);

public sealed class ServerApiStage(
  Uri baseUri,
  string streamId,
  string? authorizationToken,
  Action<ProgressArgs> progress
) : IDisposable
{
  private readonly ServerApi _serverApi = new(baseUri, authorizationToken, string.Empty);

  public long Downloaded { get; private set; }

  public async IAsyncEnumerable<Downloaded> Execute(IReadOnlyList<string> ids)
  {
    await foreach (var (id, json) in _serverApi.DownloadObjects2(streamId, ids, progress))
    {
      Downloaded++;
      yield return new Downloaded(id, json);
    }
  }

  public async ValueTask<string> DownloadRoot(string objectId)
  {
    var json = await _serverApi.DownloadSingleObject(streamId, objectId, progress).ConfigureAwait(false);
    return json.NotNull();
  }

  public void Dispose() => _serverApi.Dispose();
}
