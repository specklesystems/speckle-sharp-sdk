using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Speckle.InterfaceGenerator;
using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Api.GraphQL.Resources;
using Speckle.Sdk.Common;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Helpers;
using Speckle.Sdk.Logging;
using Speckle.Sdk.Transports;
using Speckle.Sdk.Transports.ServerUtils;

namespace Speckle.Sdk.Api.Blob;

public partial interface IBlobApi : IDisposable;

/// <summary>
/// Low level access to the blob API
/// </summary>
/// <seealso cref="FileImportResource"/>
[GenerateAutoInterface]
public sealed class BlobApi : IBlobApi
{
  public const int DEFAULT_TIMEOUT_SECONDS = SpeckleHttp.DEFAULT_TIMEOUT_SECONDS;
  private static readonly string[] s_filenameSeparator = ["filename="];

  private readonly ISdkActivityFactory _activityFactory;

  /// <summary>
  /// HTTP client for communicating with Speckle Server with auth token header
  /// </summary>
  private readonly HttpClient _authedClient;

  /// <summary>
  /// HTTP client for communicating with pre-signed s3 url
  /// </summary>
  private readonly HttpClient _unauthedClient;

  public BlobApi(
    ISpeckleHttp speckleHttp,
    ISdkActivityFactory activityFactory,
    Account account,
    int timeoutSeconds = DEFAULT_TIMEOUT_SECONDS
  )
  {
    _activityFactory = activityFactory;
    _authedClient = speckleHttp.CreateHttpClient(
      new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip },
      timeoutSeconds: timeoutSeconds,
      authorizationToken: account.token
    );
    _authedClient.BaseAddress = new(account.serverInfo.url);

    _unauthedClient = speckleHttp.CreateHttpClient(
      new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip },
      timeoutSeconds: timeoutSeconds
    );
  }

  private static string GetBlobDownloadPath(string blobId, HttpResponseMessage response)
  {
    response.Content.Headers.TryGetValues("Content-Disposition", out IEnumerable<string>? cdHeaderValues);
    var cdHeader = (cdHeaderValues?.FirstOrDefault()).NotNull(
      "Expected response from server to contain attachment header"
    );
    string fileName = cdHeader.Split(s_filenameSeparator, StringSplitOptions.None)[1].TrimStart('"').TrimEnd('"');
    return Path.Combine(
      SpecklePathProvider.BlobStoragePath(),
      $"{blobId[..Models.Blob.LocalHashPrefixLength]}-{fileName}"
    );
  }

  /// <param name="blobId">The ID of the blob to download</param>
  /// <param name="progress"></param>
  /// <param name="cancellationToken"></param>
  /// <exception cref="HttpRequestException">Request for the blob fails</exception>
  /// <exception cref="OperationCanceledException"></exception>
  /// <returns>File Path of the downloaded file</returns>
  public async Task<string> DownloadBlob(
    string projectId,
    string blobId,
    string? pathOverride = null,
    IProgress<ProgressArgs>? progress = null,
    CancellationToken cancellationToken = default
  )
  {
    using var _ = _activityFactory.Start();

    var url = new Uri($"api/stream/{projectId}/blob/{blobId}", UriKind.Relative);

    using var response = await _authedClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
    response.EnsureSuccessStatusCode();

    string fileLocation = pathOverride ?? GetBlobDownloadPath(blobId, response);
    using var source = new ProgressStream(
#if NET5_0_OR_GREATER
      await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false),
#else
      await response.Content.ReadAsStreamAsync().ConfigureAwait(false),
#endif
      response.Content.Headers.ContentLength,
      progress,
      true
    );

    using var fs = new FileStream(fileLocation, FileMode.OpenOrCreate);
#if NET5_0_OR_GREATER
    await source.CopyToAsync(fs, cancellationToken).ConfigureAwait(false);
#else
    await source.CopyToAsync(fs).ConfigureAwait(false);
#endif
    return fileLocation;
  }

  /// <summary>Queries the server for diff of the given <paramref name="blobIds"/></summary>
  /// <param name="blobIds"></param>
  /// <param name="cancellationToken"></param>
  /// <returns>A list of blob ids that the server doesn't have</returns>
  /// <exception cref="HttpRequestException">Request for the blob fails</exception>
  /// <exception cref="OperationCanceledException"></exception>
  /// <exception cref="ArgumentNullException"></exception>
  public async Task<List<string>> HasBlobs(
    string projectId,
    IReadOnlyCollection<string> blobIds,
    CancellationToken cancellationToken
  )
  {
    using var _ = _activityFactory.Start();

    cancellationToken.ThrowIfCancellationRequested();
    var payload = JsonConvert.SerializeObject(blobIds);

    var url = new Uri($"/api/stream/{projectId}/blob/diff", UriKind.Relative);

    using StringContent stringContent = new(payload, Encoding.UTF8, "application/json");

    using var response = await _authedClient.PostAsync(url, stringContent, cancellationToken).ConfigureAwait(false);
    response.EnsureSuccessStatusCode();

#if NET5_0_OR_GREATER
    var responseString = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
    var responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
    var parsed = JsonConvert
      .DeserializeObject<List<string>>(responseString)
      .NotNull($"Failed to deserialize successful response {response.Content}");

    return parsed;
  }

  /// <summary>
  /// Uploads a single file to the given S3 url.
  /// This method should be used together with the <see cref="FileImportResource"/> <see cref="FileImportResource.GenerateUploadUrl"/> method,
  /// which generates a pre-signed S3 url, that can be used to upload the file to.
  /// </summary>
  /// <param name="filePath"></param>
  /// <param name="url"></param>
  /// <param name="cancellationToken"></param>
  /// <returns>etag header</returns>
  /// <seealso cref="FileImportResource"/>
  /// <exception cref="HttpRequestException"></exception>
  /// <exception cref="ArgumentException">Unexpected response header the server</exception>
  /// <exception cref="FileNotFoundException"><paramref name="filePath"/> does not point to a file</exception>
  /// <exception cref="OperationCanceledException"></exception>
  public async Task<string> UploadFile(
    string filePath,
    Uri url,
    IProgress<ProgressArgs>? progress = null,
    CancellationToken cancellationToken = default
  )
  {
    using var _ = _activityFactory.Start();

    if (!File.Exists(filePath))
    {
      throw new FileNotFoundException("File not found.", filePath);
    }

    var fileInfo = new FileInfo(filePath);

    using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
    using var requestMessage = new HttpRequestMessage(HttpMethod.Put, url);
    requestMessage.Content = progress is null
      ? new StreamContent(fileStream)
      : new ProgressContent(new StreamContent(fileStream), progress);

    requestMessage.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
    requestMessage.Content.Headers.ContentLength = fileInfo.Length;

    using var response = await _unauthedClient.SendAsync(requestMessage, cancellationToken).ConfigureAwait(false);
    response.EnsureSuccessStatusCode();

    return ParseEtagHeader(response.Headers);
  }

  private static string ParseEtagHeader(HttpResponseHeaders headers)
  {
    if (!headers.TryGetValues("ETag", out var etagValues))
    {
      throw new ArgumentException(
        "Response does not have an ETag attached to it, cannot use this as an upload",
        nameof(headers)
      );
    }

    var etagValuesArray = etagValues.ToArray();

    if (etagValuesArray.Length != 1)
    {
      throw new ArgumentException(
        $"Expected Etag header to have a single value but got {etagValuesArray.Length}",
        nameof(headers)
      );
    }

    return etagValuesArray[0];
  }

  /// <summary>
  /// Uploads blobs via the <c>"/api/stream/:streamId/blob"</c> endpoint
  /// </summary>
  /// <param name="projectId"></param>
  /// <param name="blobPaths"></param>
  /// <param name="progress"></param>
  /// <param name="cancellationToken"></param>
  public async Task UploadBlobs(
    string projectId,
    IReadOnlyCollection<(string id, string filePath)> blobPaths,
    IProgress<ProgressArgs>? progress,
    CancellationToken cancellationToken
  )
  {
    using var _ = _activityFactory.Start();

    cancellationToken.ThrowIfCancellationRequested();
    if (blobPaths.Count == 0)
    {
      return;
    }

    using var multipartFormDataContent = new MultipartFormDataContent();
    foreach (var (id, filePath) in blobPaths)
    {
      var fileName = Path.GetFileName(filePath);

      var stream = File.OpenRead(filePath);
      var fsc = new StreamContent(stream);
      multipartFormDataContent.Add(fsc, $"hash:{id}", fileName);
    }

    using HttpContent content = progress is null
      ? multipartFormDataContent
      : new ProgressContent(multipartFormDataContent, progress);

    var url = new Uri($"/api/stream/{projectId}/blob", UriKind.Relative);

    using var response = await _authedClient.PostAsync(url, content, cancellationToken).ConfigureAwait(false);

    response.EnsureSuccessStatusCode();
  }

  [AutoInterfaceIgnore]
  public void Dispose()
  {
    _authedClient.Dispose();
    _unauthedClient.Dispose();
  }
}
