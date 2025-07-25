using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using Speckle.InterfaceGenerator;
using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Common;
using Speckle.Sdk.Helpers;
using Speckle.Sdk.Logging;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation.V2.Send;
using Speckle.Sdk.Transports;
using Speckle.Sdk.Transports.ServerUtils;

namespace Speckle.Sdk.Serialisation.V2;

public class ServerObjectManagerOptions(TimeSpan? timeout = null, string? boundary = null)
{
  public TimeSpan Timeout => timeout ?? TimeSpan.FromSeconds(120);
  public string Boundary => boundary ??= Guid.NewGuid().ToString();
}

[GenerateAutoInterface]
public class ServerObjectManager : IServerObjectManager
{
  private static readonly char[] s_separator = ['\t'];
  private static readonly string[] s_filenameSeparator = ["filename="];

  private readonly ISdkActivityFactory _activityFactory;
  private readonly HttpClient _client;
  private readonly string _projectId;
  private readonly ServerObjectManagerOptions _serverObjectManagerOptions;

  public ServerObjectManager(
    ISpeckleHttp speckleHttp,
    ISdkActivityFactory activityFactory,
    Uri baseUri,
    string projectId,
    string? authorizationToken,
    ServerObjectManagerOptions? options = null
  )
  {
    _serverObjectManagerOptions = options ?? new();
    _activityFactory = activityFactory;
    _client = speckleHttp.CreateHttpClient(
      new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip },
      timeoutSeconds: (int)_serverObjectManagerOptions.Timeout.TotalSeconds,
      authorizationToken: authorizationToken
    );
    _client.BaseAddress = baseUri;
    _projectId = projectId;
  }

  public async IAsyncEnumerable<(string, string)> DownloadObjects(
    IReadOnlyCollection<string> objectIds,
    IProgress<ProgressArgs>? progress,
    [EnumeratorCancellation] CancellationToken cancellationToken
  )
  {
    using var _ = _activityFactory.Start();
    cancellationToken.ThrowIfCancellationRequested();

    using var childrenHttpMessage = new HttpRequestMessage();
    childrenHttpMessage.RequestUri = new Uri($"/api/getobjects/{_projectId}", UriKind.Relative);
    childrenHttpMessage.Method = HttpMethod.Post;

    Dictionary<string, string> postParameters = new() { { "objects", JsonConvert.SerializeObject(objectIds) } };
    string serializedPayload = JsonConvert.SerializeObject(postParameters);
    childrenHttpMessage.Content = new StringContent(serializedPayload, Encoding.UTF8, "application/json");
    childrenHttpMessage.Headers.Add("Accept", "text/plain");

    using HttpResponseMessage childrenHttpResponse = await _client
      .SendAsync(childrenHttpMessage, HttpCompletionOption.ResponseContentRead, cancellationToken)
      .ConfigureAwait(false);

    await foreach (var (id, json) in ResponseProgress(childrenHttpResponse, progress, false, cancellationToken))
    {
      if (id is not null)
      {
        yield return (id, json);
      }
    }
  }

  public async Task<string?> DownloadSingleObject(
    string objectId,
    IProgress<ProgressArgs>? progress,
    CancellationToken cancellationToken
  )
  {
    using var _ = _activityFactory.Start();
    cancellationToken.ThrowIfCancellationRequested();

    // Get root object
    using var rootHttpMessage = new HttpRequestMessage();
    rootHttpMessage.RequestUri = new Uri($"/objects/{_projectId}/{objectId}/single", UriKind.Relative);
    rootHttpMessage.Method = HttpMethod.Get;

    using HttpResponseMessage rootHttpResponse = await _client
      .SendAsync(rootHttpMessage, HttpCompletionOption.ResponseContentRead, cancellationToken)
      .ConfigureAwait(false);

    var (_, json) = await ResponseProgress(rootHttpResponse, progress, true, cancellationToken)
      .FirstAsync()
      .ConfigureAwait(false);
    return json;
  }

  private async IAsyncEnumerable<(string?, string)> ResponseProgress(
    HttpResponseMessage childrenHttpResponse,
    IProgress<ProgressArgs>? progress,
    bool isSingle,
    [EnumeratorCancellation] CancellationToken cancellationToken
  )
  {
    childrenHttpResponse.EnsureSuccessStatusCode();
    var length = childrenHttpResponse.Content.Headers.ContentLength;
#if NET5_0_OR_GREATER
    using Stream childrenStream = await childrenHttpResponse
      .Content.ReadAsStreamAsync(cancellationToken)
      .ConfigureAwait(false);
#else
    using Stream childrenStream = await childrenHttpResponse.Content.ReadAsStreamAsync().ConfigureAwait(false);
#endif

    using var reader = new StreamReader(new ProgressStream(childrenStream, length, progress, true), Encoding.UTF8);

#if NET5_0_OR_GREATER
    while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
#else
    while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
#endif
    {
      cancellationToken.ThrowIfCancellationRequested();
      if (!isSingle)
      {
        var pcs = line.Split(s_separator, 2);
        yield return (pcs[0], pcs[1]);
      }
      else
      {
        yield return (string.Empty, line);
      }
    }
  }

  public async Task<Dictionary<string, bool>> HasObjects(
    IReadOnlyCollection<string> objectIds,
    CancellationToken cancellationToken
  )
  {
    using var _ = _activityFactory.Start();
    cancellationToken.ThrowIfCancellationRequested();
    string objectsPostParameter = JsonConvert.SerializeObject(objectIds);
    var payload = new Dictionary<string, string> { { "objects", objectsPostParameter } };
    string serializedPayload = JsonConvert.SerializeObject(payload);
    var uri = new Uri($"/api/diff/{_projectId}", UriKind.Relative);

    using StringContent stringContent = new(serializedPayload, Encoding.UTF8, "application/json");
    using HttpResponseMessage response = await _client
      .PostAsync(uri, stringContent, cancellationToken)
      .ConfigureAwait(false);

    response.EnsureSuccessStatusCode();
#if NET5_0_OR_GREATER
    var hasObjects = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
    var hasObjects = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
    return JsonConvert.DeserializeObject<Dictionary<string, bool>>(hasObjects).NotNull();
  }

  public async Task UploadObjects(
    IReadOnlyList<BaseItem> objects,
    bool compressPayloads,
    IProgress<ProgressArgs>? progress,
    CancellationToken cancellationToken
  )
  {
    using var _ = _activityFactory.Start();
    cancellationToken.ThrowIfCancellationRequested();

    using HttpRequestMessage message = new()
    {
      RequestUri = new Uri($"/objects/{_projectId}", UriKind.Relative),
      Method = HttpMethod.Post,
    };

    MultipartFormDataContent multipart = new(_serverObjectManagerOptions.Boundary);

    int mpId = 0;
    var ctBuilder = new StringBuilder("[");
    for (int i = 0; i < objects.Count; i++)
    {
      if (i > 0)
      {
        ctBuilder.Append(',');
      }

      ctBuilder.Append(objects[i].Json);
    }
    ctBuilder.Append(']');
    string ct = ctBuilder.ToString();

    if (compressPayloads)
    {
      var content = new GzipContent(new StringContent(ct, Encoding.UTF8));
      content.Headers.ContentType = new MediaTypeHeaderValue("application/gzip");
      multipart.Add(content, $"batch-{mpId}", $"batch-{mpId}");
    }
    else
    {
      multipart.Add(new StringContent(ct, Encoding.UTF8), $"batch-{mpId}", $"batch-{mpId}");
    }

    message.Content = new ProgressContent(multipart, progress);
    using HttpResponseMessage response = await _client.SendAsync(message, cancellationToken).ConfigureAwait(false);

    response.EnsureSuccessStatusCode();
  }

  /// <param name="blobId">The ID of the blob to download</param>
  /// <param name="progress"></param>
  /// <param name="cancellationToken"></param>
  /// <exception cref="HttpRequestException">Request for the blob fails</exception>
  /// <exception cref="OperationCanceledException"></exception>
  /// <returns>File Path of the downloaded file</returns>
  public async Task<string> DownloadBlob(
    string blobId,
    IProgress<ProgressArgs>? progress,
    CancellationToken cancellationToken
  )
  {
    using var _ = _activityFactory.Start();

    var url = new Uri($"api/stream/{_projectId}/blob/{blobId}", UriKind.Relative);

    using var response = await _client.GetAsync(url, cancellationToken).ConfigureAwait(false);
    response.Content.Headers.TryGetValues("Content-Disposition", out IEnumerable<string>? cdHeaderValues);
    response.EnsureSuccessStatusCode();

    var cdHeader = (cdHeaderValues?.FirstOrDefault()).NotNull(
      "Expected response from server to contain attachment header"
    );
    string fileName = cdHeader.Split(s_filenameSeparator, StringSplitOptions.None)[1].TrimStart('"').TrimEnd('"');

    string fileLocation = Path.Combine(
      SpecklePathProvider.BlobStoragePath(),
      $"{blobId[..Blob.LocalHashPrefixLength]}-{fileName}"
    );
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
  public async Task<List<string>> HasBlobs(IReadOnlyCollection<string> blobIds, CancellationToken cancellationToken)
  {
    using var _ = _activityFactory.Start();

    cancellationToken.ThrowIfCancellationRequested();
    var payload = JsonConvert.SerializeObject(blobIds);

    var url = new Uri($"/api/stream/{_projectId}/blob/diff", UriKind.Relative);

    using StringContent stringContent = new(payload, Encoding.UTF8, "application/json");

    using var response = await _client.PostAsync(url, stringContent, cancellationToken).ConfigureAwait(false);
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

  public async Task UploadBlobs(
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
      var hash = id.Split(':')[1];

      var stream = File.OpenRead(filePath);
      var fsc = new StreamContent(stream);
      multipartFormDataContent.Add(fsc, $"hash:{hash}", fileName);
    }

    using var content = new ProgressContent(multipartFormDataContent, progress);

    var url = new Uri($"/api/stream/{_projectId}/blob", UriKind.Relative);

    using var response = await _client.PostAsync(url, content, cancellationToken).ConfigureAwait(false);

    response.EnsureSuccessStatusCode();
  }
}
