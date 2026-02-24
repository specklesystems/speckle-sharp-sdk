using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Speckle.InterfaceGenerator;
using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Helpers;
using Speckle.Sdk.Pipelines.Progress;

namespace Speckle.Sdk.Pipelines.Send;

[GenerateAutoInterface]
public sealed class UploaderFactory(ISpeckleHttp httpClientFactory, ILogger<Uploader> logger) : IUploaderFactory
{
  public Uploader CreateInstance(
    string projectId,
    string ingestionId,
    Account account,
    IProgress<StreamProgressArgs> progress,
    CancellationToken cancellationToken
  ) => new(projectId, ingestionId, logger, httpClientFactory, account, progress, cancellationToken);
}

public sealed class Uploader : IDisposable
{
  private readonly string _projectId;
  private readonly string _ingestionId;
  private readonly CancellationToken _cancellationToken;
  private readonly HttpClient _speckleClient;
  private readonly HttpClient _s3Client;
  private readonly ILogger<Uploader> _logger;
  private readonly IProgress<StreamProgressArgs> _progress;

  internal Uploader(
    string projectId,
    string ingestionId,
    ILogger<Uploader> logger,
    ISpeckleHttp httpClientFactory,
    Account speckleAccount,
    IProgress<StreamProgressArgs> progress,
    CancellationToken cancellationToken
  )
  {
    _projectId = projectId;
    _ingestionId = ingestionId;
    _logger = logger;
    _cancellationToken = cancellationToken;
    _progress = progress;
    _speckleClient = httpClientFactory.CreateHttpClient(authorizationToken: speckleAccount.token);
    _speckleClient.BaseAddress = new(new(speckleAccount.serverInfo.url), "/api/v1/");

    _s3Client = httpClientFactory.CreateHttpClient();
  }

  public async Task Send(Stream fileStream)
  {
    PresignedUploadResponse presignedUpload = await GetPresignedUrl().ConfigureAwait(false);
    await UploadToS3(fileStream, presignedUpload.Url).ConfigureAwait(false);

    await TriggerProcessing().ConfigureAwait(false);
  }

  private async Task<PresignedUploadResponse> GetPresignedUrl()
  {
    var signUri = new Uri($"projects/{_projectId}/modelingestion/{_ingestionId}/uploads/sign", UriKind.Relative);

    using var signResponse = await _speckleClient.PostAsync(signUri, null, _cancellationToken).ConfigureAwait(false);
    signResponse.EnsureSuccessStatusCode();

#if NET5_0_OR_GREATER
    string signResponseString = await signResponse.Content.ReadAsStringAsync(_cancellationToken).ConfigureAwait(false);
#else
    string signResponseString = await signResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
    PresignedUploadResponse presignedUpload =
      JsonConvert.DeserializeObject<PresignedUploadResponse>(signResponseString)
      ?? throw new InvalidOperationException("Failed to get presigned upload URL");
    return presignedUpload;
  }

  private async Task UploadToS3(Stream fileStream, Uri s3Url)
  {
    _logger.LogInformation("Uploading file to pre-signed url");

    Stream progressStream = new ProgressStream(fileStream, _progress); // TODO: wrap with progress stream

    using var streamContent = new StreamContent(progressStream);
    streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
    streamContent.Headers.ContentLength = fileStream.Length;

    using var uploadRequest = new HttpRequestMessage(HttpMethod.Put, s3Url);
    uploadRequest.Content = streamContent;

    using var uploadResponse = await _s3Client
      .SendAsync(uploadRequest, HttpCompletionOption.ResponseHeadersRead, _cancellationToken)
      .ConfigureAwait(false);

    uploadResponse.EnsureSuccessStatusCode();
  }

  private async Task TriggerProcessing()
  {
    Uri processUri = new($"projects/{_projectId}/modelingestion/{_ingestionId}/uploads/process", UriKind.Relative);

    using HttpResponseMessage processResponse = await _speckleClient
      .PostAsync(processUri, null, _cancellationToken)
      .ConfigureAwait(false);

    processResponse.EnsureSuccessStatusCode();
  }

  public void Dispose()
  {
    _speckleClient.Dispose();
    _s3Client.Dispose();
  }
}
