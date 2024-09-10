using System.Collections.Concurrent;
using Speckle.Sdk.Transports.ServerUtils;

namespace Speckle.Sdk.Serialisation.Receive;

public record Transported(string Id, string Json);

public sealed class TransportStage(Uri baseUri, string streamId, string? authorizationToken) : IDisposable
{
  private readonly ServerApi _serverApi = new(baseUri, authorizationToken, string.Empty);
  private long _requested;
  private readonly ConcurrentBag<string> _requestedIds = new();

  public async ValueTask<List<Transported>> Execute(IReadOnlyList<string> ids)
  {
    var ret = new List<Transported>(ids.Count);
    foreach (var id in ids)
    {
      _requestedIds.Add(id);
    }
    await foreach (var (id, json) in _serverApi.DownloadObjects2(streamId, ids, null))
    {
      ret.Add(new Transported(id, json));
    }
    _requested += ids.Count;
    Console.WriteLine($"Transported {_requested} - Unique {_requestedIds.Count}");
    return ret;
  }

  public void Dispose() => _serverApi.Dispose();
}
