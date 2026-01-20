using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation;

namespace Speckle.Sdk.Pipeline;

public record UploadItem(string Id, Json Json, string SpeckleType, ObjectReference Reference);

public sealed class Send : IDisposable
{
  private readonly Serializer _serializer = new Serializer();
  private readonly Uploader _uploader;

  public Send()
  {
    _uploader = new Uploader("test", "test", null, null);
  }

  public async Task<ObjectReference> Process(Base @base)
  {
    var results = _serializer.Serialize(@base).ToArray();
    var first = results.First();
    foreach (var item in results)
    {
      await _uploader.PushAsync(item).ConfigureAwait(false);
    }

    return first.Reference;
  }

  public async Task WaitForUpload() => await _uploader.CompleteAsync().ConfigureAwait(false);

  public void Dispose() => _uploader.Dispose();
}
