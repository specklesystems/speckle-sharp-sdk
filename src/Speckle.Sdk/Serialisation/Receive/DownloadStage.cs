using Speckle.Sdk.Common;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Serialisation.Receive;

public record Downloaded(string Id, string Json);

public sealed class DownloadStage(Func<Downloaded, CancellationToken, ValueTask> cached, IModelSource modelSource)
  : IDisposable
{
  public long Downloaded { get; private set; }

  public async ValueTask Execute(
    IReadOnlyList<string> ids,
    Action<ProgressArgs> progress,
    CancellationToken cancellationToken
  )
  {
    await foreach (var (id, json) in modelSource.GetJsons(ids, progress).WithCancellation(cancellationToken))
    {
      Downloaded++;
      await cached(new(id, json), cancellationToken).ConfigureAwait(false);
    }
  }

  public async ValueTask<string> DownloadRoot(string objectId, Action<ProgressArgs> progress)
  {
    var json = await modelSource.GetJson(objectId, progress).ConfigureAwait(false);
    return json.NotNull();
  }

  public void Dispose() => modelSource.Dispose();
}
