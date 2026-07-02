#if NETSTANDARD2_0 || NET8_0_OR_GREATER
using Speckle.InterfaceGenerator;
using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Helpers;

namespace Speckle.Sdk.Pipelines.Receive.Artifacts;

/// <summary>
/// Downloads a Speckle 4.0 artefact bundle for a version via the v2 data endpoints — the inverse of
/// <see cref="Send.Artifacts.ArtifactPipeline"/>'s upload. Lists the bundle via
/// <c>GET /api/v2/projects/{p}/models/{m}/versions/{v}/artifacts</c> (returns presigned S3/MinIO GET urls keyed
/// by bare basename), then streams each parquet file to a local scratch directory.
/// </summary>
[GenerateAutoInterface]
public sealed class ArtifactDownloader(ISpeckleHttp httpClientFactory) : IArtifactDownloader
{
  /// <summary>
  /// Lists + downloads the version's bundle into <paramref name="destDir"/>; returns the local paths.
  /// Returns an EMPTY list when the version has no artefact bundle (not a 4.0 artefact version, or the v2
  /// data endpoints are unavailable / 404) — the caller falls back to the v1 receive path.
  /// </summary>
  public async Task<IReadOnlyList<string>> DownloadBundleAsync(
    Account account,
    string projectId,
    string modelId,
    string versionId,
    string destDir,
    CancellationToken cancellationToken
  )
  {
    using var speckleClient = httpClientFactory.CreateHttpClient(authorizationToken: account.token);
    speckleClient.BaseAddress = new(new(account.serverInfo.url), "/api/v2/");

    IReadOnlyList<ArtifactFile> files = await ListAsync(speckleClient, projectId, modelId, versionId, cancellationToken)
      .ConfigureAwait(false);
    if (files.Count == 0)
    {
      return Array.Empty<string>();
    }

    Directory.CreateDirectory(destDir);
    // Presigned urls are absolute + already authorized — use a bare client (no Speckle auth header to S3).
    using var s3Client = httpClientFactory.CreateHttpClient();
    var paths = new List<string>(files.Count);
    foreach (var file in files)
    {
      string path = Path.Combine(destDir, file.Name);
      await DownloadFileAsync(s3Client, file.Name, file.Url, path, cancellationToken).ConfigureAwait(false);
      paths.Add(path);
    }
    return paths;
  }

  private static async Task<IReadOnlyList<ArtifactFile>> ListAsync(
    HttpClient client,
    string projectId,
    string modelId,
    string versionId,
    CancellationToken cancellationToken
  )
  {
    var uri = new Uri($"projects/{projectId}/models/{modelId}/versions/{versionId}/artifacts", UriKind.Relative);
    using var response = await client.GetAsync(uri, cancellationToken).ConfigureAwait(false);
    // 404 = the endpoint/bundle isn't there (old server, or a non-artefact version) → no artefacts (v1 fallback).
    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
    {
      return Array.Empty<ArtifactFile>();
    }
    if (!response.IsSuccessStatusCode)
    {
      string body = await ReadStringAsync(response.Content, cancellationToken).ConfigureAwait(false);
      throw new HttpRequestException(
        $"Listing artefacts failed with {(int)response.StatusCode} ({response.StatusCode}): {body}"
      );
    }
    string json = await ReadStringAsync(response.Content, cancellationToken).ConfigureAwait(false);
    var parsed = JsonConvert.DeserializeObject<ArtifactsListResponse>(json);
    return parsed?.Files ?? new List<ArtifactFile>();
  }

  private static async Task DownloadFileAsync(
    HttpClient client,
    string name,
    string url,
    string path,
    CancellationToken cancellationToken
  )
  {
    using var response = await client
      .GetAsync(new Uri(url), HttpCompletionOption.ResponseHeadersRead, cancellationToken)
      .ConfigureAwait(false);
    // Name the artefact file (and echo the status) so a broken/expired presigned url is diagnosable — the bare
    // EnsureSuccessStatusCode() message ("Response status code does not indicate success: 404") hides which file
    // the list endpoint pointed at but the bucket didn't have.
    if (!response.IsSuccessStatusCode)
    {
      throw new HttpRequestException(
        $"Downloading artefact file '{name}' failed with {(int)response.StatusCode} ({response.StatusCode}). "
          + "The version's artefact manifest listed this file but the presigned download did not resolve "
          + "(missing/expired object or a server-side ingestion→version key mismatch)."
      );
    }
    using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
    await CopyToFileAsync(response.Content, fileStream, cancellationToken).ConfigureAwait(false);
  }

  // HttpContent's CancellationToken overloads are net5+; netstandard2.0 (net48) only has the no-arg forms.
  private static Task<string> ReadStringAsync(HttpContent content, CancellationToken cancellationToken) =>
#if NET8_0_OR_GREATER
    content.ReadAsStringAsync(cancellationToken);
#else
    content.ReadAsStringAsync();
#endif

  private static Task CopyToFileAsync(HttpContent content, Stream stream, CancellationToken cancellationToken) =>
#if NET8_0_OR_GREATER
    content.CopyToAsync(stream, cancellationToken);
#else
    content.CopyToAsync(stream);
#endif
}

/// <summary>Response of <c>GET …/versions/{v}/artifacts</c>: the bundle's presigned download urls.</summary>
internal sealed record ArtifactsListResponse
{
  [JsonProperty("files")]
  public List<ArtifactFile>? Files { get; init; }
}

internal sealed record ArtifactFile
{
  /// <summary>Bare artefact basename, e.g. <c>{versionId}.geometries.parquet</c>.</summary>
  [JsonProperty("name")]
  public string Name { get; init; } = "";

  /// <summary>Presigned GET url for this file.</summary>
  [JsonProperty("url")]
  public string Url { get; init; } = "";
}
#endif
