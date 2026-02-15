using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Speckle.InterfaceGenerator;
using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Common;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Helpers;

namespace Speckle.Sdk.Pipelines;

[GenerateAutoInterface]
public sealed class UploaderFactory(ISpeckleHttp httpClientFactory, ILogger<Uploader> logger) : IUploaderFactory
{
  public Uploader CreateInstance(
    string projectId,
    string modelId,
    string ingestionId,
    Account account,
    CancellationToken cancellationToken
  )
  {
    return new Uploader(projectId, modelId, ingestionId, logger, httpClientFactory, account, cancellationToken);
  }
}

public sealed class Uploader : IDisposable
{
  private readonly string _projectId;
  private readonly string _modelId;
  private readonly string _ingestionId;
  private readonly CancellationToken _cancellationToken;
  private readonly HttpClient _speckleClient;
  private readonly HttpClient _s3Client;
  private readonly Channel<UploadItem> _channel;
  private readonly Task<UploadResult> _sendTask;
  private readonly ILogger<Uploader> _logger;

  internal Uploader(
    string projectId,
    string modelId,
    string ingestionId,
    ILogger<Uploader> logger,
    ISpeckleHttp httpClientFactory,
    Account speckleAccount,
    CancellationToken cancellationToken
  )
  {
    _projectId = projectId;
    _modelId = modelId;
    _ingestionId = ingestionId;
    _logger = logger;
    _cancellationToken = cancellationToken;

    _speckleClient = httpClientFactory.CreateHttpClient(
      null,
      (int)TimeSpan.FromMinutes(30).TotalSeconds,
      speckleAccount.token
    );
    _speckleClient.BaseAddress = new(new(speckleAccount.serverInfo.url), "/api/v1/");

    _s3Client = httpClientFactory.CreateHttpClient();

    _channel = Channel.CreateBounded<UploadItem>(
      new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.Wait, SingleReader = true }
    );

    _sendTask = Task.Run(SendLoopAsync, cancellationToken);
  }

  public ValueTask PushAsync(UploadItem item) => _channel.Writer.WriteAsync(item, _cancellationToken);

  public async Task<string> CompleteAsync()
  {
    _channel.Writer.Complete();
    var result = await _sendTask.ConfigureAwait(false);
    return result.IngestionId;
  }

  private async Task<UploadResult> SendLoopAsync()
  {
    using DisposableFile tempFile = await WriteFile().ConfigureAwait(false);

    PresignedUploadResponse presignedUpload = await GetPresignedUrl().ConfigureAwait(false);
    await UploadToS3(tempFile.FileInfo, presignedUpload.Url).ConfigureAwait(false);

    ProcessUploadRequest processRequest = new() { key = presignedUpload.Key, ingestionId = _ingestionId };
    return await TriggerProcessing(processRequest).ConfigureAwait(false);
  }

  private async Task<UploadResult> TriggerProcessing(ProcessUploadRequest processRequest)
  {
    Uri processUri = new($"projects/{_projectId}/models/{_modelId}/uploads/process", UriKind.Relative);

    using StringContent content = new(JsonConvert.SerializeObject(processRequest), Encoding.UTF8, "application/json");
    using HttpResponseMessage processResponse = await _speckleClient
      .PostAsync(processUri, content, _cancellationToken)
      .ConfigureAwait(false);

    processResponse.EnsureSuccessStatusCode();

#if NET5_0_OR_GREATER
    string processResult = await processResponse.Content.ReadAsStringAsync(_cancellationToken).ConfigureAwait(false);
#else
    string processResult = await processResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
    var json = JsonConvert
      .DeserializeObject<ProcessUploadResponse>(processResult)
      .NotNull("Trigger upload processing yielded an response that failed to deserialize");

    return new UploadResult { IngestionId = json.ingestionId };
  }

  private async Task<DisposableFile> WriteFile()
  {
    string tempFilePath = Path.GetTempFileName();
    _logger.LogInformation("Writing temp file to {tempFilePath}", tempFilePath);

    using var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
    using var gzip = new GZipStream(fileStream, CompressionLevel.Optimal);
    using var writer = new StreamWriter(gzip);
    await foreach (var item in _channel.Reader.ReadAllAsync(_cancellationToken).ConfigureAwait(false))
    {
      await writer.WriteLineAsync($"{item.Id}\t{item.Json}\t{item.SpeckleType}").ConfigureAwait(false);
    }
#if NET8_0_OR_GREATER
    await writer.FlushAsync(_cancellationToken).ConfigureAwait(false);
#else
    await writer.FlushAsync().ConfigureAwait(false);
#endif
    return new DisposableFile(new FileInfo(tempFilePath), _logger);
  }

  private async Task<PresignedUploadResponse> GetPresignedUrl()
  {
    var signUri = new Uri($"projects/{_projectId}/models/{_modelId}/uploads/sign", UriKind.Relative);

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

  private async Task UploadToS3(FileInfo file, Uri s3Url)
  {
    using var fileStreamUpload = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);

    Stream progressStream = fileStreamUpload; // TODO: wrap with progress stream

    using var streamContent = new StreamContent(progressStream);
    streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
    streamContent.Headers.ContentLength = file.Length;

    using var uploadRequest = new HttpRequestMessage(HttpMethod.Put, s3Url);
    uploadRequest.Content = streamContent;

    using var uploadResponse = await _s3Client
      .SendAsync(uploadRequest, HttpCompletionOption.ResponseHeadersRead, _cancellationToken)
      .ConfigureAwait(false);

    uploadResponse.EnsureSuccessStatusCode();
  }

  public void Dispose()
  {
    _speckleClient.Dispose();
    _s3Client.Dispose();
  }
}

// DTOs
internal record PresignedUploadResponse
{
  public required Uri Url { get; init; }
  public required string Key { get; init; }
}

internal record ProcessUploadRequest
{
  public required string key { get; init; }
  public required string ingestionId { get; init; }
}

internal record ProcessUploadResponse
{
  public required string ingestionId { get; init; }
}

internal record UploadResult
{
  public required string IngestionId { get; init; }
}
