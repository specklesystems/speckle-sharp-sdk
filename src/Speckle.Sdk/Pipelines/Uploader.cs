using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Channels;
using Speckle.Newtonsoft.Json;

namespace Speckle.Sdk.Pipelines;

public sealed class Uploader : IDisposable
{
  private readonly string _projectId;
  private readonly string _modelId;
  private readonly string _ingestionId;
  private readonly CancellationToken _cancellationToken;
  private readonly HttpClient _client;
  private readonly Channel<UploadItem> _channel;
  private readonly Task<UploadResult> _sendTask;

  public Uploader(
    string projectId,
    string modelId,
    string ingestionId,
    Uri serverUrl,
    string authToken,
    CancellationToken cancellationToken
  )
  {
    _projectId = projectId;
    _modelId = modelId;
    _ingestionId = ingestionId;
    _cancellationToken = cancellationToken;
    Uri apiBaseUrl = new(serverUrl, "/api/v1/");

    _client = new HttpClient { BaseAddress = apiBaseUrl, Timeout = TimeSpan.FromMinutes(30) };

    _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

    _channel = Channel.CreateBounded<UploadItem>(
      new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.Wait, SingleReader = true }
    );

    _sendTask = SendLoopAsync();
  }

  public ValueTask PushAsync(UploadItem item, CancellationToken ct = default) => _channel.Writer.WriteAsync(item, ct);

  public async Task<string> CompleteAsync()
  {
    _channel.Writer.Complete();
    var result = await _sendTask.ConfigureAwait(false);
    return result.IngestionId;
  }

  private async Task<UploadResult> SendLoopAsync()
  {
    // 1. Stream channel to temp file
    string tempFilePath = Path.GetTempFileName();
    System.Diagnostics.Debug.WriteLine($"Temp file is at {tempFilePath}");
    try
    {
      long fileSizeBytes;
      {
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
      }
      // fileStream.Flush();
      // fileStream.Close();
      fileSizeBytes = new FileInfo(tempFilePath).Length;

      // 2. Request presigned URL
      var signUri = new Uri($"projects/{_projectId}/models/{_modelId}/uploads/sign", UriKind.Relative);

      var signResponse = await _client.GetAsync(signUri, _cancellationToken).ConfigureAwait(false);
      signResponse.EnsureSuccessStatusCode();

#if NET5_0_OR_GREATER
      string signResponseString = await signResponse
        .Content.ReadAsStringAsync(_cancellationToken)
        .ConfigureAwait(false);
#else
      string signResponseString = await signResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
      PresignedUploadResponse presignedUpload =
        JsonConvert.DeserializeObject<PresignedUploadResponse>(signResponseString)
        ?? throw new InvalidOperationException("Failed to get presigned upload URL");

      // 3. Upload to S3
      using var fileStreamUpload = new FileStream(tempFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);

      Stream progressStream = fileStreamUpload; // TODO: wrap with progress stream

      using var streamContent = new StreamContent(progressStream);
      streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
      streamContent.Headers.ContentLength = fileSizeBytes;

      using var uploadRequest = new HttpRequestMessage(HttpMethod.Put, new Uri(presignedUpload.Url, UriKind.Absolute));
      uploadRequest.Content = streamContent;

      using var s3Client = new HttpClient(); // NOTE: using a separate client for S3 as we DO NOT NEED THE AUTH HEADER, presigned url's don't work with it.
      using var uploadResponse = await s3Client
        .SendAsync(uploadRequest, HttpCompletionOption.ResponseHeadersRead, _cancellationToken)
        .ConfigureAwait(false);

      uploadResponse.EnsureSuccessStatusCode();

      // 4. Trigger processing
      var processUri = new Uri($"projects/{_projectId}/models/{_modelId}/uploads/process", UriKind.Relative);
      var processRequest = new ProcessUploadRequest { key = presignedUpload.Key, ingestionId = _ingestionId };

      using StringContent content = new(JsonConvert.SerializeObject(processRequest), Encoding.UTF8, "application/json");
      var processResponse = await _client.PostAsync(processUri, content, _cancellationToken).ConfigureAwait(false);

      processResponse.EnsureSuccessStatusCode();

#if NET5_0_OR_GREATER
      string processResult = await processResponse.Content.ReadAsStringAsync(_cancellationToken).ConfigureAwait(false);
#else
      string processResult = await processResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
      var json = JsonConvert.DeserializeObject<ProcessUploadResponse>(processResult);

      if (json is null)
      {
        throw new InvalidOperationException("Failed to trigger upload processing");
      }

      return new UploadResult { IngestionId = json.ingestionId };
    }
    finally
    {
      // 5. Clean up temp file
      if (File.Exists(tempFilePath))
      {
        try
        {
          //File.Delete(tempFilePath);
        }
#pragma warning disable CA1031
        catch
#pragma warning restore CA1031
        {
          // Best effort
        }
      }
    }
  }

  public void Dispose() => _client.Dispose();
}

// DTOs
internal record PresignedUploadResponse
{
  public required string Url { get; init; }
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
