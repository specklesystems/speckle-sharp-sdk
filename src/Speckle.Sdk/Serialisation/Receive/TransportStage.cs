using System.Collections.Concurrent;
using Speckle.Sdk.Transports.ServerUtils;

namespace Speckle.Sdk.Serialisation.Receive;

public record Transported(string Id, string Json);

public sealed class TransportStage : IDisposable
{
  private readonly ServerApi _serverApi;
  private readonly string _streamId;
  private long _requested;
  private ConcurrentBag<string> _requestedIds = new();

  public TransportStage(Uri baseUri, string streamId, string? authorizationToken)
  {
    _streamId = streamId;
    _serverApi = new(baseUri, authorizationToken, string.Empty);
  }

  public async ValueTask<List<Transported>> Execute(IReadOnlyList<string> ids)
  {
    var ret = new List<Transported>(ids.Count);
    try
    {
      foreach (var id in ids)
      {
        _requestedIds.Add(id);
      }
      await foreach(var (id, json) in _serverApi.DownloadObjects2(_streamId, ids, null))
      {
        ret.Add(new Transported(
          id,
          json
        ));
      }
      _requested += ids.Count;
      Console.WriteLine($"Transported {_requested} - Unique {_requestedIds.Count}");
    }
    catch (Exception ex)
    {
      Console.WriteLine(ex);
      throw;
    }
    return ret;
  }

  public void Dispose() => _serverApi.Dispose();
}
