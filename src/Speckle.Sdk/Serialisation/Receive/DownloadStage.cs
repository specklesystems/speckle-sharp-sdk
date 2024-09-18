using Speckle.Sdk.Common;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Serialisation.Receive;

public record Downloaded(string Id, string Json);

public sealed class DownloadStage(Func<Downloaded, ValueTask> cached, IModelSource modelSource) : IDisposable
{
  public long Downloaded { get; private set; }

  public async ValueTask Execute(IReadOnlyList<string> ids, Action<ProgressArgs> progress)
  {
    await foreach (var (id, json) in modelSource.GetJsons(ids, progress))
    {
      Downloaded++;
      await cached(new(id, json)).ConfigureAwait(false);
    }
  }

  public async ValueTask<string> DownloadRoot(string objectId, Action<ProgressArgs> progress)
  {
    var json = await modelSource.GetJson(objectId, progress).ConfigureAwait(false);
    return json.NotNull();
  }

  public void Dispose() => modelSource.Dispose();
}
