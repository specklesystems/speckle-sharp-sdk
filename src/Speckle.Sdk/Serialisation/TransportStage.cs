using System.Threading.Channels;
using Speckle.Sdk.Transports.ServerUtils;

namespace Speckle.Sdk.Serialisation;

public record Transported(string Id, string Json);
public sealed class TransportStage : Stage<string, Transported>, IDisposable
{ 
  private readonly ServerApi _serverApi;
  private readonly string _streamId;
  public TransportStage(Uri baseUri, string streamId, string? authorizationToken)
    : base(Channel.CreateUnbounded<string>(), ServerApi.BATCH_SIZE_GET_OBJECTS)
  {
    _streamId = streamId;
    _serverApi = new (baseUri, authorizationToken, string.Empty);
  }

  protected override async ValueTask<IReadOnlyList<Transported>> Execute(IReadOnlyList<string> ids)
  {
    var ret = new List<Transported>(ids.Count);
    ret.AddRange(
      (await _serverApi.DownloadObjects2(_streamId, ids, null).ConfigureAwait(false)).Select(x =>
        new Transported(x.Item1, x.Item2)));
     return ret;
  }

  public void Dispose() => _serverApi.Dispose();
}
