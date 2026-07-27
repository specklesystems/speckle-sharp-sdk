using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Speckle.Sdk.Artifacts.Harness;

/// <summary>
/// Client for the server's bundle-migration API, which uploads a produced bundle onto an EXISTING version
/// (no ingestion, no new version):
/// <c>POST /api/v1/projects/{projectId}/models/{modelId}/versions/{versionId}/migration/uploads</c>.
///
/// The credential is the per-job migration JWT (passed as <c>SPECKLE_TOKEN</c> by the migration service),
/// sent as the bearer token; its claims must match the target project/model/version exactly. The presigned
/// URLs it returns are self-authenticating and must be used WITHOUT that token.
///
/// The migration service owns the surrounding lifecycle (<c>start</c> / <c>complete</c> / <c>fail</c>);
/// this client only signs and uploads.
/// </summary>
internal sealed class BundleMigrationClient(ILogger<BundleMigrationClient> logger)
{
  // The server presigns at most this many files per call.
  private const int MAX_FILES_PER_CALL = 100;

  /// <summary>One presigned PUT target. <paramref name="AdditionalRequestHeaders"/> is absent on S3/MinIO
  /// but required on Azure, so it must be merged into the PUT.</summary>
  internal sealed record PresignedUpload(
    string FileName,
    string Url,
    IReadOnlyDictionary<string, string>? AdditionalRequestHeaders
  );

  /// <summary>Response of the migration uploads endpoint: a presigned PUT per requested file, plus a
  /// presigned GET of the source packfile (direct from storage, bypassing the server pods).</summary>
  internal sealed record UploadTargets(IReadOnlyList<PresignedUpload> Uploads, string PackfileDownloadUrl);

  /// <summary>
  /// Requests presigned uploads for <paramref name="files"/>. Pass an EMPTY list to fetch only
  /// <see cref="UploadTargets.PackfileDownloadUrl"/> — the source packfile is needed before the produced
  /// filenames are known, so the flow calls this twice. Batches in chunks of 100 (the server's cap);
  /// presigning creates no objects, so repeat calls are harmless.
  /// </summary>
  public async Task<UploadTargets> RequestUploadsAsync(
    Uri server,
    string projectId,
    string modelId,
    string versionId,
    IReadOnlyList<string> files,
    string token,
    CancellationToken ct
  )
  {
    var url = new Uri(
      server,
      $"/api/v1/projects/{projectId}/models/{modelId}/versions/{versionId}/migration/uploads"
    );

    using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    // Cloudflare 403s (code 1010) requests with no User-Agent.
    http.DefaultRequestHeaders.UserAgent.ParseAdd("speckle-artefact-harness/1.0");

    var uploads = new List<PresignedUpload>(files.Count);
    string? packfileDownloadUrl = null;

    // An empty list still needs one call (that's how the packfile url is obtained).
    for (var offset = 0; offset == 0 || offset < files.Count; offset += MAX_FILES_PER_CALL)
    {
      var batch = files.Skip(offset).Take(MAX_FILES_PER_CALL).ToArray();
      var (batchUploads, packfileUrl) = await PostUploadsAsync(http, url, batch, ct).ConfigureAwait(false);
      uploads.AddRange(batchUploads);
      packfileDownloadUrl ??= packfileUrl;
    }

    if (packfileDownloadUrl is null)
    {
      throw new InvalidOperationException("migration uploads response did not include a packfileDownloadUrl.");
    }
    return new UploadTargets(uploads, packfileDownloadUrl);
  }

  private async Task<(List<PresignedUpload> Uploads, string? PackfileDownloadUrl)> PostUploadsAsync(
    HttpClient http,
    Uri url,
    string[] files,
    CancellationToken ct
  )
  {
    logger.LogInformation("Requesting migration uploads for {FileCount} file(s)", files.Length);

    var body = JsonSerializer.Serialize(new { files });
    using var content = new StringContent(body, Encoding.UTF8, "application/json");
    using var resp = await http.PostAsync(url, content, ct).ConfigureAwait(false);
    var respBody = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

    if (!resp.IsSuccessStatusCode)
    {
      // An empty `files` list is how we fetch the packfile url; older servers still enforce a minimum of
      // one file and reject it, which is otherwise easy to mistake for an auth or url problem.
      if (files.Length == 0 && resp.StatusCode == HttpStatusCode.BadRequest)
      {
        throw new InvalidOperationException(
          "migration uploads rejected an empty 'files' list (400). The server must allow it so the "
            + $"packfile download url can be fetched before the bundle exists. Response: {respBody}"
        );
      }
      throw new InvalidOperationException($"migration uploads failed ({(int)resp.StatusCode}): {respBody}");
    }

    return ParseUploadsResponse(respBody);
  }

  private static (List<PresignedUpload> Uploads, string? PackfileDownloadUrl) ParseUploadsResponse(string body)
  {
    using var doc = JsonDocument.Parse(body);
    var root = doc.RootElement;

    var uploads = new List<PresignedUpload>();
    if (root.TryGetProperty("uploads", out var uploadsElement))
    {
      foreach (var entry in uploadsElement.EnumerateArray())
      {
        var fileName = entry.GetProperty("fileName").GetString()!;
        var url = entry.GetProperty("url").GetString()!;

        Dictionary<string, string>? headers = null;
        if (
          entry.TryGetProperty("additionalRequestHeaders", out var headersElement)
          && headersElement.ValueKind == JsonValueKind.Object
        )
        {
          headers = new Dictionary<string, string>(StringComparer.Ordinal);
          foreach (var header in headersElement.EnumerateObject())
          {
            headers[header.Name] = header.Value.ToString();
          }
        }

        uploads.Add(new PresignedUpload(fileName, url, headers));
      }
    }

    var packfileUrl = root.TryGetProperty("packfileDownloadUrl", out var p) ? p.GetString() : null;
    return (uploads, packfileUrl);
  }

  /// <summary>
  /// PUTs one produced bundle file to its presigned url. <paramref name="http"/> must be an auth-free
  /// client: presigned urls carry their own auth in the query string, and a bearer header alongside it is
  /// rejected (MinIO: "multiple authentication types").
  /// </summary>
  public async Task PutFileAsync(HttpClient http, PresignedUpload target, string filePath, CancellationToken ct)
  {
    await using var stream = File.OpenRead(filePath);
    using var content = new StreamContent(stream);
    content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
    content.Headers.ContentLength = stream.Length;

    using var req = new HttpRequestMessage(HttpMethod.Put, target.Url) { Content = content };
    if (target.AdditionalRequestHeaders is { } extraHeaders)
    {
      foreach (var header in extraHeaders)
      {
        req.Headers.TryAddWithoutValidation(header.Key, header.Value);
      }
    }

    using var resp = await http.SendAsync(req, ct).ConfigureAwait(false);
    if (!resp.IsSuccessStatusCode)
    {
      var errBody = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
      throw new InvalidOperationException(
        $"PUT {target.FileName} failed ({(int)resp.StatusCode}): {errBody}"
      );
    }
    logger.LogInformation("PUT {FileName} {Bytes} bytes", target.FileName, stream.Length);
  }
}
