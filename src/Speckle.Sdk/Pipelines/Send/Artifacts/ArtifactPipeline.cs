#if NETSTANDARD2_0 || NET8_0_OR_GREATER
using System.Net.Http.Headers;
using System.Text;
using Speckle.InterfaceGenerator;
using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Helpers;
using Speckle.Sdk.Logging;

namespace Speckle.Sdk.Pipelines.Send.Artifacts;

[GenerateAutoInterface]
public sealed class ArtifactPipelineFactory(ISpeckleHttp httpClientFactory, ISdkActivityFactory activityFactory)
  : IArtifactPipelineFactory
{
  public ArtifactPipeline CreateInstance(
    string projectId,
    string ingestionId,
    string versionId,
    Account account,
    string outputDir,
    CancellationToken cancellationToken
  ) => new(projectId, ingestionId, versionId, account, outputDir, httpClientFactory, activityFactory, cancellationToken);
}

/// <summary>
/// The Speckle 4.0 artifact pipeline: uploads a client-built artefact bundle (the parquet triple —
/// <c>geometries.parquet</c> + <c>eav.*.parquet</c> + <c>envelope.*.parquet</c>, produced by
/// <c>ObjectsArtifactPipeline</c>) via the v2 data endpoints — sign → presigned PUT per file →
/// complete (which creates the version). Fully independent of the v1 <see cref="SendPipeline"/>;
/// neither side touches the other.
/// </summary>
/// <remarks>
/// <para>The server v2 endpoints are <b>filename-keyed and count-agnostic</b>: sign takes the list of
/// artefact basenames, the server presigns one PUT per name under <c>versions/{versionId}/{name}</c>, and
/// complete verifies the etags and creates the commit with <c>id = versionId</c>. The <see cref="VersionId"/>
/// is <b>pre-allocated by the server at ingestion creation</b>, so the producer can bake it into the artefact
/// filenames before any bytes exist.</para>
/// <para>Usage: build the artefact bundle externally (via <c>ObjectsArtifactPipeline</c>), then call
/// <see cref="UploadFilesAsync"/> once.</para>
/// </remarks>
public sealed class ArtifactPipeline : IDisposable
{
  private readonly string _projectId;
  private readonly string _ingestionId;
  private readonly CancellationToken _cancellationToken;
  private readonly HttpClient _speckleClient;
  private readonly HttpClient _s3Client;
  private readonly ISdkActivityFactory _activity;

  /// <summary>
  /// The server pre-allocated version id this pipeline uploads under. Same id the producer bakes into
  /// artefact filenames, the server uses as the S3 key prefix, and the commit PK at complete.
  /// </summary>
  public string VersionId { get; }

  /// <summary>The local directory the artefact bundle was written to (the producer's output dir).</summary>
  public string OutputDir { get; }

  internal ArtifactPipeline(
    string projectId,
    string ingestionId,
    string versionId,
    Account account,
    string outputDir,
    ISpeckleHttp httpClientFactory,
    ISdkActivityFactory activityFactory,
    CancellationToken cancellationToken
  )
  {
    _projectId = projectId;
    _ingestionId = ingestionId;
    VersionId = versionId;
    _cancellationToken = cancellationToken;
    _activity = activityFactory;
    // Local artefact files are named by the pre-allocated versionId (final names from byte one — no
    // placeholder, no server-side rename). The ingestionId keys the sign/complete endpoint URLs.
    OutputDir = outputDir;

    _speckleClient = httpClientFactory.CreateHttpClient(authorizationToken: account.token);
    _speckleClient.BaseAddress = new(new(account.serverInfo.url), "/api/v2/");

    _s3Client = httpClientFactory.CreateHttpClient();
  }

  /// <summary>
  /// Uploads an already-built artefact bundle (<b>filename → local path</b>) via the v2
  /// sign → PUT → complete flow. Count-agnostic — pass however many files the bundle has (the
  /// dictionary keys are the basenames the server presigns and keys under <c>versions/{versionId}/</c>).
  /// The server creates the version with the given <paramref name="rootId"/> (which may be a synthetic id —
  /// the artefact path has no serialized root).
  /// </summary>
  public Task<string> UploadFilesAsync(
    IReadOnlyDictionary<string, string> fileNameToPath,
    string rootId,
    int totalChildrenCount
  )
  {
    using var a = _activity.Start("Uploading artefact bundle (v2)");
    try
    {
      return UploadByFileNameAsync(fileNameToPath, rootId, totalChildrenCount);
    }
    catch (Exception ex)
    {
      a?.SetStatus(SdkActivityStatusCode.Error);
      a?.RecordException(ex);
      throw;
    }
  }

  /// <summary>
  /// Shared sign → PUT-per-file → complete core. <paramref name="fileNameToPath"/> maps
  /// each artefact basename to its local path; the server signs/keys per filename.
  /// </summary>
  private async Task<string> UploadByFileNameAsync(
    IReadOnlyDictionary<string, string> fileNameToPath,
    string rootId,
    int totalChildrenCount
  )
  {
    var signed = await Sign(fileNameToPath.Keys.ToArray()).ConfigureAwait(false);

    var etags = new Dictionary<string, string>();
    foreach (var kvp in fileNameToPath)
    {
      if (!signed.Uploads.TryGetValue(kvp.Key, out var presigned))
      {
        throw new InvalidOperationException($"Server did not sign an upload for file '{kvp.Key}'");
      }
      MemoryLog.Phase($"pipeline: PUT {kvp.Key}");
      etags[kvp.Key] = await UploadFile(kvp.Value, presigned).ConfigureAwait(false);
    }

    MemoryLog.Phase("pipeline: complete request");
    var versionId = await Complete(etags, rootId, totalChildrenCount).ConfigureAwait(false);
    MemoryLog.Log("pipeline: version completed");
    return versionId;
  }

  private async Task<ArtifactsSignResponse> Sign(string[] files)
  {
    var uri = new Uri($"projects/{_projectId}/modelingestion/{_ingestionId}/uploads/sign", UriKind.Relative);
    var body = JsonConvert.SerializeObject(new ArtifactsSignRequest { Files = files });
    using var content = new StringContent(body, Encoding.UTF8, "application/json");
    using var response = await _speckleClient.PostAsync(uri, content, _cancellationToken).ConfigureAwait(false);
    await EnsureSuccessWithBody(response, "artifacts sign").ConfigureAwait(false);

    var responseString = await ReadBodyAsync(response).ConfigureAwait(false);
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

  private async Task<string> Complete(Dictionary<string, string> etags, string rootId, int totalChildrenCount)
  {
    var uri = new Uri($"projects/{_projectId}/modelingestion/{_ingestionId}/uploads/complete", UriKind.Relative);
    var body = JsonConvert.SerializeObject(
      new ArtifactsCompleteRequest
      {
        Etags = etags,
        RootId = rootId,
        TotalChildrenCount = totalChildrenCount,
      }
    );
    using var content = new StringContent(body, Encoding.UTF8, "application/json");
    using var response = await _speckleClient.PostAsync(uri, content, _cancellationToken).ConfigureAwait(false);
    await EnsureSuccessWithBody(response, "artifacts complete").ConfigureAwait(false);

    // The version id is pre-allocated (server-minted at ingestion creation) and is the commit PK the
    // server just completed against — so it is authoritative. We return it rather than depending on the
    // complete response echoing it (it may legitimately return no body). If the server DOES echo a
    // versionId it must match; a mismatch means we'd point at the wrong version, so fail loudly.
    var responseString = await ReadBodyAsync(response).ConfigureAwait(false);
    var echoed = TryReadEchoedVersionId(responseString);
    if (echoed is not null && !string.Equals(echoed, VersionId, StringComparison.Ordinal))
    {
      throw new InvalidOperationException(
        $"Server completed version '{echoed}' but the pre-allocated id was '{VersionId}'."
      );
    }
    return VersionId;
  }

  // HttpContent.ReadAsStringAsync(CancellationToken) is net5+; netstandard2.0 (the net48 plugin build) only
  // has the no-arg overload. The token still bounds the surrounding PostAsync, so dropping it here is benign.
  private Task<string> ReadBodyAsync(HttpResponseMessage response) =>
#if NET8_0_OR_GREATER
    response.Content.ReadAsStringAsync(_cancellationToken);
#else
    response.Content.ReadAsStringAsync();
#endif

  private static string? TryReadEchoedVersionId(string responseString)
  {
    if (string.IsNullOrWhiteSpace(responseString))
    {
      return null;
    }
    return JsonConvert.DeserializeObject<ArtifactsCompleteResponse>(responseString)?.VersionId;
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
    var body = await ReadBodyAsync(response).ConfigureAwait(false);
    throw new HttpRequestException($"{operation} failed with {(int)response.StatusCode} ({response.StatusCode}): {body}");
  }

  public void Dispose()
  {
    _speckleClient.Dispose();
    _s3Client.Dispose();
  }
}

/// <summary>Response of the v2 artifacts sign endpoint: one presigned PUT per artefact filename.</summary>
internal record ArtifactsSignResponse
{
  [JsonProperty("uploads")]
  public required Dictionary<string, PresignedUploadResponse> Uploads { get; init; }
}

/// <summary>Request body for the v2 sign endpoint: the artefact basenames to presign (count-agnostic).</summary>
internal readonly struct ArtifactsSignRequest
{
  [JsonProperty("files")]
  public required string[] Files { get; init; }
}

internal readonly struct ArtifactsCompleteRequest
{
  /// <summary>etag per artefact filename (matches the basenames sent to sign).</summary>
  [JsonProperty("etags")]
  public required Dictionary<string, string> Etags { get; init; }

  [JsonProperty("rootId")]
  public required string RootId { get; init; }

  [JsonProperty("totalChildrenCount")]
  public required int TotalChildrenCount { get; init; }
}

/// <summary>
/// Optional response of the v2 complete endpoint. The version id is pre-allocated, so the server need not
/// echo it; when present it is cross-checked against the pre-allocated id.
/// </summary>
internal record ArtifactsCompleteResponse
{
  [JsonProperty("versionId")]
  public string? VersionId { get; init; }
}
#endif
