using System.Net.Http.Headers;
using System.Text;
using Speckle.InterfaceGenerator;
using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Helpers;
using Speckle.Sdk.Logging;
using Speckle.Sdk.Pipelines.Progress;

namespace Speckle.Sdk.Pipelines.Send;

[GenerateAutoInterface]
public sealed class UploaderFactory(ISpeckleHttp httpClientFactory, ISdkActivityFactory activityFactory)
  : IUploaderFactory
{
  public Uploader CreateInstance(
    string projectId,
    string ingestionId,
    Account account,
    IProgress<StreamProgressArgs> progress,
    CancellationToken cancellationToken
  ) => new(projectId, ingestionId, activityFactory, httpClientFactory, account, progress, cancellationToken);
}

public sealed class Uploader : IDisposable
{
  private readonly string _projectId;
  private readonly string _ingestionId;
  private readonly CancellationToken _cancellationToken;
  private readonly HttpClient _speckleClient;
  private readonly HttpClient _s3Client;
  private readonly ISdkActivityFactory _activity;
  private readonly IProgress<StreamProgressArgs> _progress;

  internal Uploader(
    string projectId,
    string ingestionId,
    ISdkActivityFactory activity,
    ISpeckleHttp httpClientFactory,
    Account speckleAccount,
    IProgress<StreamProgressArgs> progress,
    CancellationToken cancellationToken
  )
  {
    _projectId = projectId;
    _ingestionId = ingestionId;
    _activity = activity;
    _cancellationToken = cancellationToken;
    _progress = progress;
    _speckleClient = httpClientFactory.CreateHttpClient(authorizationToken: speckleAccount.token);
    _speckleClient.BaseAddress = new(new(speckleAccount.serverInfo.url), "/api/v1/");

    _s3Client = httpClientFactory.CreateHttpClient();
  }

  public async Task Send(Stream fileStream)
  {
    PresignedUploadResponse presignedUploadResponse = await GetPresignedUrl().ConfigureAwait(false);
    var etag = await UploadToS3(fileStream, presignedUploadResponse).ConfigureAwait(false);

    await TriggerProcessing(new() { Etag = etag }).ConfigureAwait(false);
  }

  private async Task<PresignedUploadResponse> GetPresignedUrl()
  {
    using var a = _activity.Start("Get Presigned Url");

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

  private async Task<string> UploadToS3(Stream fileStream, PresignedUploadResponse presignedUploadResponse)
  {
    using var a = _activity.Start("Uploading file to pre-signed url");

    Stream progressStream = new ProgressStream(fileStream, _progress);

    using var streamContent = new StreamContent(progressStream);
    streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
    streamContent.Headers.ContentLength = fileStream.Length;

    using var uploadRequest = new HttpRequestMessage(HttpMethod.Put, presignedUploadResponse.Url);
    foreach (var kvp in presignedUploadResponse.AdditionalRequestHeaders)
    {
      uploadRequest.Headers.Add(kvp.Key, kvp.Value);
    }

    uploadRequest.Content = streamContent;

    using var uploadResponse = await _s3Client
      .SendAsync(uploadRequest, HttpCompletionOption.ResponseHeadersRead, _cancellationToken)
      .ConfigureAwait(false);

    uploadResponse.EnsureSuccessStatusCode();

    return BlobApiHelpers.ParseEtagHeader(uploadResponse.Headers);
  }

  private async Task TriggerProcessing(TriggerUploadRequest request)
  {
    using var a = _activity.Start("Triggering Processing");

    Uri processUri = new($"projects/{_projectId}/modelingestion/{_ingestionId}/uploads/process", UriKind.Relative);
    string requestBody = JsonConvert.SerializeObject(request);
    using var content = new StringContent(requestBody, Encoding.UTF8, "application/json");

    using HttpResponseMessage processResponse = await _speckleClient
      .PostAsync(processUri, content, _cancellationToken)
      .ConfigureAwait(false);

    string body = await processResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
    processResponse.EnsureSuccessStatusCode();
  }

  public void Dispose()
  {
    _speckleClient.Dispose();
    _s3Client.Dispose();
  }
}
