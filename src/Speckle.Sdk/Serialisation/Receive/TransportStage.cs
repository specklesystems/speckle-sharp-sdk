using Speckle.Sdk.Transports.ServerUtils;

namespace Speckle.Sdk.Serialisation.Receive;

public record Transported(string Id, string Json);

public sealed class TransportStage(Uri baseUri, string streamId, string? authorizationToken) : IDisposable
{
  private readonly ServerApi _serverApi = new(baseUri, authorizationToken, string.Empty);

  public long Downloaded { get; private set; }

  public async IAsyncEnumerable<Transported> Execute(IReadOnlyList<string> ids)
  {
    await foreach (var (id, json) in _serverApi.DownloadObjects2(streamId, ids, null))
    {
      Downloaded++;
      yield return new Transported(id, json);
    }
  }

  public void Dispose() => _serverApi.Dispose();
}
