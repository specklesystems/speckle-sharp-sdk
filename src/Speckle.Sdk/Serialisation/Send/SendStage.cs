using Speckle.Sdk.Transports.ServerUtils;

namespace Speckle.Sdk.Serialisation;

public sealed class SendStage : IDisposable
{
  private readonly ServerApi _serverApi;
  private readonly string _streamId;

  public SendStage(Uri baseUri, string streamId, string? authorizationToken)
  {
    _streamId = streamId;
    _serverApi = new(baseUri, authorizationToken, string.Empty);
  }

  public async ValueTask Execute(List<Serialized> serialized)
  {
    var hasResults = await _serverApi
      .HasObjects(_streamId, serialized.Select(x => x.Id).ToArray())
      .ConfigureAwait(false);

    await _serverApi
      .UploadObjects(
        _streamId,
        serialized.Where(x => hasResults.ContainsKey(x.Id)).Select(x => (x.Id, x.Json)).ToArray(),
        args => { }
      )
      .ConfigureAwait(false);
  }

  public void Dispose() => _serverApi.Dispose();
}
