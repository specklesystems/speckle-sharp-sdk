using Speckle.Sdk.Common;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Serialisation.Receive;

public record Downloaded(string Id, string Json);

public sealed class GatherStage(IModelSource modelSource) : IDisposable
{
  public long Downloaded { get; private set; }

  public async IAsyncEnumerable<Downloaded> Execute(IReadOnlyList<string> ids, Action<ProgressArgs> progress)
  {
    await foreach (var (id, json) in modelSource.GetJsons(ids, progress))
    {
      Downloaded++;
      yield return new Downloaded(id, json);
    }
  }

  public async ValueTask<string> DownloadRoot(string objectId, Action<ProgressArgs> progress)
  {
    var json = await modelSource.GetJson(objectId, progress).ConfigureAwait(false);
    return json.NotNull();
  }

  public void Dispose() => modelSource.Dispose();
}
