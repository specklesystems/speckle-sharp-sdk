using System.Collections.Concurrent;
using Speckle.Sdk.Transports;
using Speckle.Sdk.Transports.ServerUtils;

namespace Speckle.Sdk.Serialisation.Send;

public interface IModelTarget : IDisposable
{
  ValueTask Send(IReadOnlyList<(string, string)> objects, Action<ProgressArgs> progress);
}

public sealed class ServerTarget : IModelTarget
{
  private readonly ServerApi _serverApi;
  private readonly string _streamId;

  public ServerTarget(Uri baseUri, string streamId, string? authorizationToken)
  {
    _streamId = streamId;
    _serverApi = new(baseUri, authorizationToken, string.Empty);
  }

  public async ValueTask Send(IReadOnlyList<(string, string)> objects, Action<ProgressArgs> progress)
  {
    await _serverApi.UploadObjects(_streamId, objects, progress).ConfigureAwait(false);
  }

  public void Dispose() => _serverApi.Dispose();
}

public sealed class SendStage(IModelTarget modelTarget) : IDisposable
{
  public long Sent { get; private set; }

  public async ValueTask Execute(List<Serialized> serialized)
  {
    await modelTarget
      .Send(
        serialized.Select(x => (x.Id, x.Json)).ToArray(),
        args =>
        {
          Sent += args.Count ?? 0;
        }
      )
      .ConfigureAwait(false);
  }

  public void Dispose() => modelTarget.Dispose();
}

public sealed class MemoryTarget : IModelTarget
{
  public ConcurrentDictionary<string, string> Sent { get; } = new();

  public ValueTask Send(IReadOnlyList<(string, string)> objects, Action<ProgressArgs> progress)
  {
    foreach (var (id, json) in objects)
    {
      Sent.TryAdd(id, json);
    }
    return new(Task.CompletedTask);
  }

  public void Dispose() { }
}
