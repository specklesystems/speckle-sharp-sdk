using Speckle.Sdk.Credentials;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation;

namespace Speckle.Sdk.Pipelines;

public record UploadItem(string Id, Json Json, string SpeckleType, ObjectReference Reference);

public sealed class SendPipeline : IDisposable
{
  private readonly CancellationToken _cancellationToken;
  private readonly Serializer _serializer = new();
  private readonly Uploader _uploader;

  public SendPipeline(
    Account account,
    string projectId,
    string modelId,
    string ingestionId,
    CancellationToken cancellationToken
  )
  {
    _cancellationToken = cancellationToken;
    _uploader = new Uploader(projectId, modelId, ingestionId, account.serverInfo.url, account.token, cancellationToken);
  }

  private UploadItem _lastItem;

  public async Task<ObjectReference> Process(Base @base)
  {
    var results = _serializer.Serialize(@base).ToArray();
    var first = results.First();
    foreach (var item in results)
    {
      // we're not doing fire and forget here so that we get the backpressure from the uploader
      await _uploader.PushAsync(item).ConfigureAwait(false);
    }

    // NOTE: this is important to keep track of. When we serialze an object, we get back a list of objects, with the first one being the original root.
    // In the case of the commit root object, this means the last object is not necessarily the root; we therefore need to manually track its existance here
    // and ensure it's the last one through in the uploader's stream. See WaitForUpload down below.
    _lastItem = first;
    return first.Reference;
  }

  public async Task WaitForUpload()
  {
    await _uploader.PushAsync(_lastItem).ConfigureAwait(false);
    await _uploader.CompleteAsync().ConfigureAwait(false);
  }

  public async Task<string> WaitForUploadAndServerProcessing()
  {
    // TODO: in some way, wait for the server to process the upload and return the actual new version id
    return await Task.FromResult("todo").ConfigureAwait(false);
  }

  public void Dispose() => _uploader.Dispose();
}
