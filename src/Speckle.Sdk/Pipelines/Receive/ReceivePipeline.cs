using Microsoft.Extensions.Logging;
using Speckle.InterfaceGenerator;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Helpers;
using Speckle.Sdk.Logging;
using Speckle.Sdk.Models;
using Speckle.Sdk.Pipelines.Progress;
using Version = Speckle.Sdk.Api.GraphQL.Models.Version;

namespace Speckle.Sdk.Pipelines.Receive;

[GenerateAutoInterface]
public sealed class ReceivePipelineFactory(
  ISpeckleHttp speckleHttp,
  ILogger<ReceivePipeline> logger,
  ISdkActivityFactory activityFactory
) : IReceivePipelineFactory
{
  public ReceivePipeline CreateInstance(Version version, Model model, Project project, Account account)
  {
    return CreateInstance(version.id, model.id, project.id, account);
  }

  public ReceivePipeline CreateInstance(string versionId, string modelId, string projectId, Account account)
  {
    var httpClient = speckleHttp.CreateHttpClient(authorizationToken: account.token);
    httpClient.BaseAddress = new(account.serverInfo.url, UriKind.Absolute);

    return new ReceivePipeline(httpClient, versionId, modelId, projectId, logger, activityFactory);
  }
}

public sealed class ReceivePipeline(
  HttpClient client,
  string versionId,
  string modelId,
  string projectId,
  ILogger<ReceivePipeline> logger,
  ISdkActivityFactory activityFactory
) : IDisposable
{
  public async Task<Base> Receive(
    IProgress<StreamProgressArgs> downloadProgress,
    CancellationToken cancellationToken
  ) => await ReceiveSerial(downloadProgress, cancellationToken).ConfigureAwait(false);

  public async Task<Base> ReceiveSerial(
    IProgress<StreamProgressArgs> downloadProgress,
    CancellationToken cancellationToken
  )
  {
    using var activity = activityFactory.Start();
    try
    {
      using var tempFile = new DisposableFile(new FileInfo(Path.GetTempFileName()), logger);
      await DownloadDuckFile(tempFile.FileInfo, downloadProgress, cancellationToken).ConfigureAwait(false);

      using PackFileManager packFileManager = new(tempFile.FileInfo, activityFactory);
      var deserializer = new SpeckleObjectDeserializer(packFileManager);

      // string rootObject = packFileManager.GetRootObjectId();
      // return packFileManager.GetObjectsDepthFirst().ToArray();
      Base result = deserializer.GetCompleteObjectsTreeSerial(); //TODO: cancellation
      activity?.SetStatus(SdkActivityStatusCode.Ok);

      return result;
    }
    catch (Exception ex)
    {
      activity?.SetStatus(SdkActivityStatusCode.Error);
      activity?.RecordException(ex);
      throw;
    }
  }

  public async Task DownloadDuckFile(
    FileInfo destination,
    IProgress<StreamProgressArgs> downloadProgress,
    CancellationToken cancellationToken
  )
  {
    Uri url = new($"/api/v1/projects/{projectId}/models/{modelId}/versions/{versionId}/download", UriKind.Relative);

    using var activity = activityFactory.Start();
    try
    {
      using var response = await client
        .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
        .ConfigureAwait(false);
      response.EnsureSuccessStatusCode();

      using var destinationStream = new FileStream(
        destination.FullName,
        FileMode.Create,
        FileAccess.Write,
        FileShare.None,
        1024 * 1024,
        FileOptions.Asynchronous
      );
      using ProgressStream progressStream = new(destinationStream, downloadProgress);

#if NET5_0_OR_GREATER
      await response.Content.CopyToAsync(destinationStream, null, cancellationToken).ConfigureAwait(false);
#else
      await response.Content.CopyToAsync(destinationStream).ConfigureAwait(false);
#endif
      destination.Refresh();
      activity?.SetStatus(SdkActivityStatusCode.Ok);
    }
    catch (Exception ex)
    {
      activity?.SetStatus(SdkActivityStatusCode.Error);
      activity?.RecordException(ex);
      throw;
    }
  }

  public void Dispose() => client.Dispose();
}
