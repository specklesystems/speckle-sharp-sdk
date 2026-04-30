using Speckle.InterfaceGenerator;
using Speckle.Sdk.Api.GraphQL.Models;
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
    ModelIngestion ingestion,
    Account account,
    IProgress<StreamProgressArgs> uploadProgress,
    CancellationToken cancellationToken
  )
  {
    var uploader = uploaderFactory.CreateInstance(
      ingestion.projectId,
      ingestion.id,
      account,
      uploadProgress,
      cancellationToken
    );
    var diskStore = diskStoreFactory.CreateInstance(cancellationToken);
    return new SendPipeline(uploader, diskStore);
  }

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
  internal Uploader Uploader { get; }
  internal DiskStore DiskStore { get; }

  internal SendPipeline(Uploader uploader, DiskStore diskStore)
  {
    Uploader = uploader;
    DiskStore = diskStore;
  }

  public async Task<ObjectReference> Process(Base @base)
  {
    var results = _serializer.Serialize(@base).ToArray();
    foreach (var item in results.Reverse())
    {
      // we're not doing fire and forget here so that we get the backpressure from the uploader
      await DiskStore.PushAsync(item).ConfigureAwait(false);
    }

    return results.First().Reference;
  }

  public async Task WaitForUpload()
  {
    using DisposableFile tempFile = await DiskStore.CompleteAsync().ConfigureAwait(false);

    using Stream fileStreamUpload = new FileStream(
      tempFile.FileInfo.FullName,
      FileMode.Open,
      FileAccess.Read,
      FileShare.Read
    );

    await Uploader.Send(fileStreamUpload).ConfigureAwait(false);
  }

  public void Dispose() => Uploader.Dispose();
}
