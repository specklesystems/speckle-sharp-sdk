using Speckle.InterfaceGenerator;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Helpers;
using Speckle.Sdk.Models;
using Speckle.Sdk.Pipelines.Progress;

namespace Speckle.Sdk.Pipelines.Send;

[GenerateAutoInterface]
public sealed class SendPipelineFactory(IUploaderFactory uploaderFactory, IDiskStoreFactory diskStoreFactory)
  : ISendPipelineFactory
{
  public SendPipeline CreateInstance(
    string projectId,
    string ingestionId,
    Account account,
    IProgress<StreamProgressArgs> uploadProgress,
    CancellationToken cancellationToken
  )
  {
    var uploader = uploaderFactory.CreateInstance(projectId, ingestionId, account, uploadProgress, cancellationToken);
    var diskStore = diskStoreFactory.CreateInstance(cancellationToken);
    return new SendPipeline(uploader, diskStore);
  }
}

public sealed class SendPipeline : IDisposable
{
  private readonly Serializer _serializer = new();
  private readonly Uploader _uploader;
  private readonly DiskStore _diskStore;

  internal SendPipeline(Uploader uploader, DiskStore diskStore)
  {
    _uploader = uploader;
    _diskStore = diskStore;
  }

  public async Task<ObjectReference> Process(Base @base)
  {
    var results = _serializer.Serialize(@base).ToArray();
    foreach (var item in results.Reverse())
    {
      // we're not doing fire and forget here so that we get the backpressure from the uploader
      await _diskStore.PushAsync(item).ConfigureAwait(false);
    }

    return results.First().Reference;
  }

  public async Task WaitForUpload()
  {
    //await _diskStore.PushAsync(_lastItem).ConfigureAwait(false);
    using DisposableFile tempFile = await _diskStore.CompleteAsync().ConfigureAwait(false);

    using Stream fileStreamUpload = new FileStream(
      tempFile.FileInfo.FullName,
      FileMode.Open,
      FileAccess.Read,
      FileShare.Read
    );

    await _uploader.Send(fileStreamUpload).ConfigureAwait(false);
  }

  public void Dispose() => _uploader.Dispose();
}
