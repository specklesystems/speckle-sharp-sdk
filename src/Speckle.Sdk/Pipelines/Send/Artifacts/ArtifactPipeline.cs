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
/// via the v2 endpoints (sign → presigned PUT per file → complete, which
/// creates the version). Fully independent of the v1 <see cref="SendPipeline"/>
/// — neither side touches the other.
/// </summary>
/// <remarks>
/// Usage: call <see cref="Process"/> for every object (the LAST processed
/// object is recorded as the root, same convention as the server), then
/// <see cref="UploadAsync"/> once.
/// </remarks>
public sealed class ArtifactPipeline : IDisposable
{
  private const string VIEWER_PURPOSE = "viewer";
  private const string EAV_PURPOSE = "eav";

  private readonly string _outputDir;
  private readonly string _projectId;
  private readonly string _ingestionId;
  private readonly CancellationToken _cancellationToken;
  private readonly HttpClient _speckleClient;
  private readonly HttpClient _s3Client;
  private readonly ISdkActivityFactory _activity;

  // Created lazily on first use. The envelope path triggers them via Process()
  // (serialize → id + __closure) and UploadAsync() (viewer/eav writer). The
  // full-binary path uses ONLY UploadFilesAsync(), so it constructs NEITHER —
  // no id/closure serialization, no DuckDbArtifactWriter, no empty viewer/eav
  // files. This keeps the binary flow free of all serialization machinery.
  private SerializerV2? _serializer;
  private DuckDbArtifactWriter? _writer;

  // The artifact pipeline uses the closure-free 4.0 serializer (SerializerV2):
  // no object generates __closure. The legacy closure-based Serializer is left
  // intact for other/comparison uses. id + JSON + speckle_type still emitted.
  private SerializerV2 Serializer => _serializer ??= new();
  private DuckDbArtifactWriter Writer => _writer ??= new DuckDbArtifactWriter(_outputDir, _ingestionId);

  /// <summary>Local viewer.duckdb path once written (null if nothing was processed).</summary>
  public string? ViewerDbPath => _writer?.ViewerDbPath;

  /// <summary>Local eav.duckdb path once written (null if nothing was processed).</summary>
  public string? EavDbPath => _writer?.EavDbPath;

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
    // Local files mirror the staging-key convention: {ingestionId}.{purpose}.duckdb.
    // The writer itself is created lazily (see Writer) so the binary path builds none.
    _outputDir = outputDir;

    _speckleClient = httpClientFactory.CreateHttpClient(authorizationToken: account.token);
    _speckleClient.BaseAddress = new(new(account.serverInfo.url), "/api/v2/");

    _s3Client = httpClientFactory.CreateHttpClient();
  }

  private string? _rootId;
  private int _objectCount;

  /// <summary>
  /// Routes one object (and its detached children) into the artifact files and
  /// returns the object's reference (callers use these to build collections).
  /// </summary>
  public ObjectReference Process(Base @base)
  {
    var results = Serializer.Serialize(@base).ToArray();
    var first = results.First();
    // The first item is the processed object itself; the LAST Process call is
    // the root collection by convention, so this converges on the root id.
    _rootId = first.Id;
    _objectCount += results.Length;
    // .Reverse ensures children precede their parent and the root of this
    // call lands last — the writer treats the overall last item as the root.
    foreach (var item in results.Reverse())
    {
      Writer.Add(item);
    }

    return first.Reference;
  }

  /// <summary>
  /// Finalizes the files and uploads them: v2 sign → PUT per file → v2
  /// complete. The server verifies etags, then creates the version for this
  /// ingestion (renaming the staged files to their version-keyed names).
  /// </summary>
  /// <returns>The version id the server created (or reused) for this ingestion.</returns>
  public async Task<string> UploadAsync()
  {
    if (_rootId is null)
    {
      throw new InvalidOperationException("No objects were processed; nothing to upload.");
    }

    using var a = _activity.Start("Uploading duckdb artifacts (v2)");
    try
    {
      MemoryLog.Log("pipeline: UploadAsync begin");
      Writer.Complete();

      var files = new Dictionary<string, string>
      {
        [VIEWER_PURPOSE] = Writer.ViewerDbPath,
        [EAV_PURPOSE] = Writer.EavDbPath,
      };

      var signed = await Sign(files.Keys.ToArray()).ConfigureAwait(false);

      var etags = new Dictionary<string, string>();
      foreach (var kvp in files)
      {
        if (!signed.Uploads.TryGetValue(kvp.Key, out var presigned))
        {
          throw new InvalidOperationException($"Server did not sign an upload for purpose '{kvp.Key}'");
        }
        MemoryLog.Phase($"pipeline: PUT {kvp.Key}");
        etags[kvp.Key] = await UploadFile(kvp.Value, presigned).ConfigureAwait(false);
      }

      MemoryLog.Phase("pipeline: complete request");
      var versionId = await Complete(etags, _rootId, _objectCount).ConfigureAwait(false);
      MemoryLog.Log("pipeline: version completed");
      return versionId;
    }
    catch (Exception ex)
    {
      a?.SetStatus(SdkActivityStatusCode.Error);
      a?.RecordException(ex);
      throw;
    }
  }

  /// <summary>
  /// Uploads an already-built set of artifact files (purpose → local path) via
  /// the same v2 sign → PUT → complete flow as <see cref="UploadAsync"/>, but
  /// WITHOUT this pipeline's own <see cref="DuckDbArtifactWriter"/>. Used by the
  /// full-binary path to upload objects.duckdb + eav.duckdb, reusing the
  /// account / project / ingestion / HTTP context this pipeline carries. The
  /// server creates the version with the given <paramref name="rootId"/>
  /// (which may be a synthetic id — the binary path has no serialized root).
  /// </summary>
  public async Task<string> UploadFilesAsync(
    IReadOnlyDictionary<string, string> purposeToFilePath,
    string rootId,
    int totalChildrenCount
  )
  {
    using var a = _activity.Start("Uploading binary duckdb artifacts (v2)");
    try
    {
      var signed = await Sign(purposeToFilePath.Keys.ToArray()).ConfigureAwait(false);

      var etags = new Dictionary<string, string>();
      foreach (var kvp in purposeToFilePath)
      {
        if (!signed.Uploads.TryGetValue(kvp.Key, out var presigned))
        {
          throw new InvalidOperationException($"Server did not sign an upload for purpose '{kvp.Key}'");
        }
        etags[kvp.Key] = await UploadFile(kvp.Value, presigned).ConfigureAwait(false);
      }

      return await Complete(etags, rootId, totalChildrenCount).ConfigureAwait(false);
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

    var responseString = await response.Content.ReadAsStringAsync(_cancellationToken).ConfigureAwait(false);
    var completed =
      JsonConvert.DeserializeObject<ArtifactsCompleteResponse>(responseString)
      ?? throw new InvalidOperationException("Failed to parse artifacts complete response");
    return completed.VersionId;
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
    _writer?.Dispose(); // null on the binary path (never created)
    _speckleClient.Dispose();
    _s3Client.Dispose();
  }
}

/// <summary>Response of the v2 artifacts sign endpoint: one presigned upload per purpose (staging keys).</summary>
internal record ArtifactsSignResponse
{
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
  [JsonProperty("etags")]
  public required Dictionary<string, string> Etags { get; init; }

  [JsonProperty("rootId")]
  public required string RootId { get; init; }

  [JsonProperty("totalChildrenCount")]
  public required int TotalChildrenCount { get; init; }
}

/// <summary>Response of the v2 complete endpoint: the version the server created (or reused) for the ingestion.</summary>
internal record ArtifactsCompleteResponse
{
  [JsonProperty("versionId")]
  public required string VersionId { get; init; }
}
#endif
