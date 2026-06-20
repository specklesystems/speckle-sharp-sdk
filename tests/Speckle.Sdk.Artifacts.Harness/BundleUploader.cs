using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Speckle.Sdk.Artifacts.Harness;

/// <summary>
/// Uploads a produced parquet bundle to a Speckle server via the v2 envelope-bundle
/// upload flow:
///   1. GraphQL createModelIngestion → reserved versionId + ingestionId
///   2. POST .../uploads/sign  { files:[basenames] } → { uploads: { name:{url,key} } }
///   3. PUT each file to its presigned url, capturing the ETag header
///   4. POST .../uploads/complete { etags, rootId, totalChildrenCount? } → { versionId }
///
/// Endpoint paths derived from server modules/data/rest/upload.ts (router mounted at
/// API_PATH = '/api', routes literally '/v2/projects/:projectId/modelingestion/:ingestionId/uploads/{sign,complete}').
/// </summary>
public static class BundleUploader
{
  public sealed record UploadResult(string IngestionId, string VersionId, IReadOnlyList<string> Files);

  public static async Task<UploadResult> UploadAsync(
    string serverUrl,
    string projectId,
    string modelId,
    string outDir,
    string rootId,
    int? totalChildrenCount,
    string token,
    CancellationToken ct
  )
  {
    var baseUrl = serverUrl.TrimEnd('/');
    using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    // ── 1. create the model ingestion → reserved versionId + ingestionId ─────────────
    var (ingestionId, reservedVersionId) = await CreateModelIngestionAsync(
      http,
      baseUrl,
      projectId,
      modelId,
      ct
    ).ConfigureAwait(false);
    Console.WriteLine($"Ingestion created: {ingestionId}  (reserved versionId {reservedVersionId})");

    // ── files to upload: every flat file the producer wrote into outDir ───────────────
    var files = Directory.GetFiles(outDir).Select(Path.GetFileName).Where(n => n is not null).Cast<string>().ToList();
    if (files.Count == 0)
    {
      throw new InvalidOperationException($"No bundle files found in {outDir}.");
    }

    // ── 2. sign — one presigned PUT per file ──────────────────────────────────────────
    var signUrl = $"{baseUrl}/api/v2/projects/{projectId}/modelingestion/{ingestionId}/uploads/sign";
    var signBody = JsonSerializer.Serialize(new { files });
    using var signResp = await PostJsonAsync(http, signUrl, signBody, ct).ConfigureAwait(false);
    var signRespBody = await signResp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
    if (!signResp.IsSuccessStatusCode)
    {
      throw new InvalidOperationException(
        $"sign failed ({(int)signResp.StatusCode}): {signRespBody}"
      );
    }

    var uploads = ParseSignResponse(signRespBody);

    // ── 3. PUT each file, capture ETag ────────────────────────────────────────────────
    // Presigned S3/MinIO PUTs are self-authenticating via the query string. The shared `http`
    // client carries a default Bearer header (for the server API) which MinIO rejects as
    // "multiple authentication types" — so PUT through a separate, auth-free client.
    using var s3Http = new HttpClient();
    var etags = new Dictionary<string, string>(StringComparer.Ordinal);
    foreach (var filename in files)
    {
      if (!uploads.TryGetValue(filename, out var presigned))
      {
        throw new InvalidOperationException($"sign response missing url for '{filename}'.");
      }
      var etag = await PutFileAsync(s3Http, presigned.Url, Path.Combine(outDir, filename), ct)
        .ConfigureAwait(false);
      etags[filename] = etag;
      Console.WriteLine($"  PUT {filename,-36} etag {etag}");
    }

    // ── 4. complete — creates the commit (schemaVersion 3) ────────────────────────────
    var completeUrl =
      $"{baseUrl}/api/v2/projects/{projectId}/modelingestion/{ingestionId}/uploads/complete";
    var completeBody = JsonSerializer.Serialize(
      new
      {
        etags,
        rootId,
        totalChildrenCount
      }
    );
    using var completeResp = await PostJsonAsync(http, completeUrl, completeBody, ct)
      .ConfigureAwait(false);
    var completeRespBody = await completeResp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
    if (!completeResp.IsSuccessStatusCode)
    {
      throw new InvalidOperationException(
        $"complete failed ({(int)completeResp.StatusCode}): {completeRespBody}"
      );
    }

    using var completeDoc = JsonDocument.Parse(completeRespBody);
    var versionId = completeDoc.RootElement.GetProperty("versionId").GetString()!;
    return new UploadResult(ingestionId, versionId, files);
  }

  // GraphQL: projectMutations.modelIngestionMutations.create(input) → ModelIngestion { id, versionId }
  private static async Task<(string ingestionId, string versionId)> CreateModelIngestionAsync(
    HttpClient http,
    string baseUrl,
    string projectId,
    string modelId,
    CancellationToken ct
  )
  {
    const string mutation =
      @"mutation CreateIngestion($input: ModelIngestionCreateInput!) {
  projectMutations {
    modelIngestionMutations {
      create(input: $input) {
        id
        versionId
      }
    }
  }
}";

    var variables = new
    {
      input = new
      {
        projectId,
        modelId,
        progressMessage = "Artefact bundle upload (harness)",
        sourceData = new
        {
          sourceApplicationSlug = "artefact-harness",
          sourceApplicationVersion = "v2"
        }
      }
    };

    var payload = JsonSerializer.Serialize(new { query = mutation, variables });
    using var resp = await PostJsonAsync(http, $"{baseUrl}/graphql", payload, ct).ConfigureAwait(false);
    var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
    if (!resp.IsSuccessStatusCode)
    {
      throw new InvalidOperationException(
        $"createModelIngestion failed ({(int)resp.StatusCode}): {body}"
      );
    }

    using var doc = JsonDocument.Parse(body);
    if (doc.RootElement.TryGetProperty("errors", out var errors))
    {
      throw new InvalidOperationException($"createModelIngestion GraphQL errors: {errors}");
    }

    var created = doc
      .RootElement.GetProperty("data")
      .GetProperty("projectMutations")
      .GetProperty("modelIngestionMutations")
      .GetProperty("create");

    var ingestionId = created.GetProperty("id").GetString()!;
    var versionId = created.GetProperty("versionId").GetString()
      ?? throw new InvalidOperationException(
        "createModelIngestion returned a null versionId (pre-v2 ingestion?)."
      );
    return (ingestionId, versionId);
  }

  private sealed record Presigned(string Url, string Key);

  private static Dictionary<string, Presigned> ParseSignResponse(string body)
  {
    using var doc = JsonDocument.Parse(body);
    var uploads = doc.RootElement.GetProperty("uploads");
    var result = new Dictionary<string, Presigned>(StringComparer.Ordinal);
    foreach (var entry in uploads.EnumerateObject())
    {
      var url = entry.Value.GetProperty("url").GetString()!;
      var key = entry.Value.TryGetProperty("key", out var k) ? (k.GetString() ?? "") : "";
      result[entry.Name] = new Presigned(url, key);
    }
    return result;
  }

  private static async Task<string> PutFileAsync(
    HttpClient http,
    string presignedUrl,
    string filePath,
    CancellationToken ct
  )
  {
    await using var stream = File.OpenRead(filePath);
    using var content = new StreamContent(stream);
    content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

    // Presigned PUT urls carry their own auth in the query string — do NOT send our bearer token.
    using var req = new HttpRequestMessage(HttpMethod.Put, presignedUrl) { Content = content };
    using var resp = await http.SendAsync(req, ct).ConfigureAwait(false);
    if (!resp.IsSuccessStatusCode)
    {
      var errBody = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
      throw new InvalidOperationException(
        $"PUT {Path.GetFileName(filePath)} failed ({(int)resp.StatusCode}): {errBody}"
      );
    }

    var etag = resp.Headers.ETag?.Tag;
    if (string.IsNullOrEmpty(etag) && resp.Headers.TryGetValues("ETag", out var values))
    {
      etag = values.FirstOrDefault();
    }
    if (string.IsNullOrEmpty(etag))
    {
      throw new InvalidOperationException(
        $"PUT {Path.GetFileName(filePath)} succeeded but returned no ETag header."
      );
    }
    // S3 returns the etag quoted (e.g. "\"abc\""); the server compares against the storage
    // metadata eTag verbatim. Send it exactly as received (quotes preserved).
    return etag;
  }

  private static Task<HttpResponseMessage> PostJsonAsync(
    HttpClient http,
    string url,
    string json,
    CancellationToken ct
  )
  {
    var content = new StringContent(json, Encoding.UTF8, "application/json");
    return http.PostAsync(url, content, ct);
  }
}
