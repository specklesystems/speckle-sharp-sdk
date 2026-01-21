using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation;

namespace Speckle.Sdk.Pipeline;

public record UploadItem(string Id, Json Json, string SpeckleType, ObjectReference Reference);

public sealed class Send : IDisposable
{
  private readonly Serializer _serializer = new();
  private readonly Uploader _uploader;

  // TODO: parametetrise as this as required below
  public Send()
  {
    _uploader = new Uploader("test", "test", null, null);
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

    _lastItem = first;
    return first.Reference;
  }

  public async Task WaitForUpload()
  {
    // I don't like this, but it's the only way to make sure the last item is sent correctly. Needs more investigation as to why.
    // Observed behaviour on the server: the last item pushed in the channel always comes second last, with the last one being the previous one.
    await _uploader.PushAsync(_lastItem).ConfigureAwait(false);
    await _uploader.CompleteAsync().ConfigureAwait(false);
  }

  public void Dispose() => _uploader.Dispose();
}
