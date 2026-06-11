#if NET8_0_OR_GREATER
using System.Net.Http.Headers;
using System.Text;
using Speckle.InterfaceGenerator;
using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Helpers;
using Speckle.Sdk.Logging;
using Speckle.Sdk.Models;

namespace Speckle.Sdk.Pipelines.Send.Artifacts;

[GenerateAutoInterface]
public sealed class ArtifactPipelineFactory(ISpeckleHttp httpClientFactory, ISdkActivityFactory activityFactory)
  : IArtifactPipelineFactory
{
  public ArtifactPipeline CreateInstance(
    string projectId,
    string ingestionId,
    Account account,
    string outputDir,
    CancellationToken cancellationToken
  ) => new(projectId, ingestionId, account, outputDir, httpClientFactory, activityFactory, cancellationToken);
}

/// <summary>
/// The Speckle 4.0 artifact pipeline: serializes objects into the
/// purpose-specific DuckDB files (viewer + eav) client-side and uploads them
/// via the v2 endpoints (sign → presigned PUT per file → complete). Fully
/// independent of the v1 <see cref="SendPipeline"/> — callers run both during
/// dual-write and can drop either side without touching the other.
/// </summary>
/// <remarks>
/// Usage: call <see cref="Process"/> for every object that also goes through
/// the v1 pipeline (the LAST processed object is recorded as the root, same
/// convention as the server), then <see cref="UploadAsync"/> once.
/// The cost of independence is a second serialization pass over the model;
/// acceptable for the POC, revisit if profiling says otherwise.
/// </remarks>
public sealed class ArtifactPipeline : IDisposable
{
  private const string VIEWER_PURPOSE = "viewer";
  private const string EAV_PURPOSE = "eav";

  private readonly Serializer _serializer = new();
  private readonly DuckDbArtifactWriter _writer;
  private readonly string _projectId;
  private readonly string _ingestionId;
  private readonly CancellationToken _cancellationToken;
  private readonly HttpClient _speckleClient;
  private readonly HttpClient _s3Client;
  private readonly ISdkActivityFactory _activity;

  internal ArtifactPipeline(
    string projectId,
    string ingestionId,
    Account account,
    string outputDir,
    ISpeckleHttp httpClientFactory,
    ISdkActivityFactory activityFactory,
    CancellationToken cancellationToken
  )
  {
    _projectId = projectId;
    _ingestionId = ingestionId;
    _cancellationToken = cancellationToken;
    _activity = activityFactory;
    _writer = new DuckDbArtifactWriter(outputDir, $"{projectId}_{ingestionId}");

    _speckleClient = httpClientFactory.CreateHttpClient(authorizationToken: account.token);
    _speckleClient.BaseAddress = new(new(account.serverInfo.url), "/api/v2/");

    _s3Client = httpClientFactory.CreateHttpClient();
  }

  /// <summary>Routes one object (and its detached children) into the artifact files.</summary>
  public void Process(Base @base)
  {
    var results = _serializer.Serialize(@base).ToArray();
    // .Reverse ensures children precede their parent and the root of this
    // call lands last — the writer treats the overall last item as the root.
    foreach (var item in results.Reverse())
    {
      _writer.Add(item);
    }
  }

  /// <summary>
  /// Finalizes the files and uploads them: v2 sign (server mints the version
  /// id) → PUT per file → v2 complete (server verifies etags).
  /// </summary>
  /// <returns>The server-minted version id the artifacts were stored under.</returns>
  public async Task<string> UploadAsync()
  {
    using var a = _activity.Start("Uploading duckdb artifacts (v2)");
    try
    {
      _writer.Complete();

      var files = new Dictionary<string, string>
      {
        [VIEWER_PURPOSE] = _writer.ViewerDbPath,
        [EAV_PURPOSE] = _writer.EavDbPath,
      };

      var signed = await Sign(files.Keys.ToArray()).ConfigureAwait(false);

      var etags = new Dictionary<string, string>();
      foreach (var kvp in files)
      {
        if (!signed.Uploads.TryGetValue(kvp.Key, out var presigned))
        {
          throw new InvalidOperationException($"Server did not sign an upload for purpose '{kvp.Key}'");
        }
        etags[kvp.Key] = await UploadFile(kvp.Value, presigned).ConfigureAwait(false);
      }

      await Complete(signed.VersionId, etags).ConfigureAwait(false);
      return signed.VersionId;
    }
    catch (Exception ex)
    {
      a?.SetStatus(SdkActivityStatusCode.Error);
      a?.RecordException(ex);
      throw;
    }
  }

  private async Task<ArtifactsSignResponse> Sign(string[] purposes)
  {
    var uri = new Uri($"projects/{_projectId}/modelingestion/{_ingestionId}/uploads/sign", UriKind.Relative);
    var body = JsonConvert.SerializeObject(new ArtifactsSignRequest { Purposes = purposes });
    using var content = new StringContent(body, Encoding.UTF8, "application/json");
    using var response = await _speckleClient.PostAsync(uri, content, _cancellationToken).ConfigureAwait(false);
    await EnsureSuccessWithBody(response, "artifacts sign").ConfigureAwait(false);

    var responseString = await response.Content.ReadAsStringAsync(_cancellationToken).ConfigureAwait(false);
    return JsonConvert.DeserializeObject<ArtifactsSignResponse>(responseString)
      ?? throw new InvalidOperationException("Failed to get presigned artifact upload URLs");
  }

  private async Task<string> UploadFile(string filePath, PresignedUploadResponse presigned)
  {
    using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
    using var streamContent = new StreamContent(fileStream);
    streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
    streamContent.Headers.ContentLength = fileStream.Length;

    using var uploadRequest = new HttpRequestMessage(HttpMethod.Put, presigned.Url);
    foreach (var kvp in presigned.AdditionalRequestHeaders)
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

  private async Task Complete(string versionId, Dictionary<string, string> etags)
  {
    var uri = new Uri($"projects/{_projectId}/modelingestion/{_ingestionId}/uploads/complete", UriKind.Relative);
    var body = JsonConvert.SerializeObject(new ArtifactsCompleteRequest { VersionId = versionId, Etags = etags });
    using var content = new StringContent(body, Encoding.UTF8, "application/json");
    using var response = await _speckleClient.PostAsync(uri, content, _cancellationToken).ConfigureAwait(false);
    await EnsureSuccessWithBody(response, "artifacts complete").ConfigureAwait(false);
  }

  /// <summary>
  /// Like EnsureSuccessStatusCode, but includes the response body in the
  /// exception so server-side validation errors (400s) are diagnosable.
  /// </summary>
  private async Task EnsureSuccessWithBody(HttpResponseMessage response, string operation)
  {
    if (response.IsSuccessStatusCode)
    {
      return;
    }
    var body = await response.Content.ReadAsStringAsync(_cancellationToken).ConfigureAwait(false);
    throw new HttpRequestException($"{operation} failed with {(int)response.StatusCode} ({response.StatusCode}): {body}");
  }

  public void Dispose()
  {
    _writer.Dispose();
    _speckleClient.Dispose();
    _s3Client.Dispose();
  }
}

/// <summary>Response of the v2 artifacts sign endpoint: server-minted version id + one presigned upload per purpose.</summary>
internal record ArtifactsSignResponse
{
  [JsonProperty("versionId")]
  public required string VersionId { get; init; }

  [JsonProperty("uploads")]
  public required Dictionary<string, PresignedUploadResponse> Uploads { get; init; }
}

internal readonly struct ArtifactsSignRequest
{
  [JsonProperty("purposes")]
  public required string[] Purposes { get; init; }
}

internal readonly struct ArtifactsCompleteRequest
{
  [JsonProperty("versionId")]
  public required string VersionId { get; init; }

  [JsonProperty("etags")]
  public required Dictionary<string, string> Etags { get; init; }
}
#endif
