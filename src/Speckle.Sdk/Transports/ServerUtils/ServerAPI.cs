using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Speckle.Newtonsoft.Json;
using Speckle.Newtonsoft.Json.Linq;
using Speckle.Sdk.Common;
using Speckle.Sdk.Helpers;
using Speckle.Sdk.Logging;
using Speckle.Sdk.Models;

namespace Speckle.Sdk.Transports.ServerUtils;

public sealed class ServerApi : IDisposable, IServerApi
{
  private const int BATCH_SIZE_GET_OBJECTS = 10000;
  private const int BATCH_SIZE_HAS_OBJECTS = 100000;

  private const int MAX_MULTIPART_COUNT = 5;
  private const int MAX_MULTIPART_SIZE = 25_000_000;
  private const int MAX_OBJECT_SIZE = 25_000_000;

  private const int MAX_REQUEST_SIZE = 100_000_000;

  private static readonly char[] s_separator = { '\t' };
  private static readonly string[] s_filenameSeparator = { "filename=" };

  private readonly HttpClient _client;

  public ServerApi(Uri baseUri, string? authorizationToken, string blobStorageFolder, int timeoutSeconds = 120)
  {
    CancellationToken = CancellationToken.None;

    BlobStorageFolder = blobStorageFolder;

    _client = Http.GetHttpProxyClient(
      new SpeckleHttpClientHandler(
        new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip },
        Http.HttpAsyncPolicy(timeoutSeconds: timeoutSeconds)
      )
    );
    _client.BaseAddress = baseUri;

    Http.AddAuthHeader(_client, authorizationToken);
  }

  public CancellationToken CancellationToken { get; set; }
  public bool CompressPayloads { get; set; } = true;

  public string BlobStorageFolder { get; set; }

  public void Dispose()
  {
    _client.Dispose();
  }

  public async Task<string?> DownloadSingleObject(string streamId, string objectId, Action<ProgressArgs>? progress)
  {
    using var _ = SpeckleActivityFactory.Start();
    CancellationToken.ThrowIfCancellationRequested();

    // Get root object
    using var rootHttpMessage = new HttpRequestMessage
    {
      RequestUri = new Uri($"/objects/{streamId}/{objectId}/single", UriKind.Relative),
      Method = HttpMethod.Get
    };

    HttpResponseMessage rootHttpResponse = await _client
      .SendAsync(rootHttpMessage, HttpCompletionOption.ResponseContentRead, CancellationToken)
      .ConfigureAwait(false);

    string? rootObjectStr = null;
    await ResponseProgress(rootHttpResponse, progress, (_, json) => rootObjectStr = json, true).ConfigureAwait(false);
    return rootObjectStr;
  }

  public async Task DownloadObjects(
    string streamId,
    IReadOnlyList<string> objectIds,
    Action<ProgressArgs>? progress,
    CbObjectDownloaded onObjectCallback
  )
  {
    if (objectIds.Count == 0)
    {
      return;
    }
    using var _ = SpeckleActivityFactory.Start();

    if (objectIds.Count < BATCH_SIZE_GET_OBJECTS)
    {
      await DownloadObjectsImpl(streamId, objectIds, progress, onObjectCallback).ConfigureAwait(false);
      return;
    }

    List<string> crtRequest = new();
    foreach (string id in objectIds)
    {
      if (crtRequest.Count >= BATCH_SIZE_GET_OBJECTS)
      {
        await DownloadObjectsImpl(streamId, crtRequest, progress, onObjectCallback).ConfigureAwait(false);
        crtRequest = new List<string>();
      }
      crtRequest.Add(id);
    }
    await DownloadObjectsImpl(streamId, crtRequest, progress, onObjectCallback).ConfigureAwait(false);
  }

  public async Task<Dictionary<string, bool>> HasObjects(string streamId, IReadOnlyList<string> objectIds)
  {
    if (objectIds.Count <= BATCH_SIZE_HAS_OBJECTS)
    {
      return await HasObjectsImpl(streamId, objectIds).ConfigureAwait(false);
    }

    Dictionary<string, bool> ret = new();
    List<string> crtBatch = new(BATCH_SIZE_HAS_OBJECTS);
    foreach (string objectId in objectIds)
    {
      crtBatch.Add(objectId);
      if (crtBatch.Count >= BATCH_SIZE_HAS_OBJECTS)
      {
        Dictionary<string, bool> batchResult = await HasObjectsImpl(streamId, crtBatch).ConfigureAwait(false);
        foreach (KeyValuePair<string, bool> kv in batchResult)
        {
          ret[kv.Key] = kv.Value;
        }

        crtBatch = new List<string>(BATCH_SIZE_HAS_OBJECTS);
      }
    }
    if (crtBatch.Count > 0)
    {
      Dictionary<string, bool> batchResult = await HasObjectsImpl(streamId, crtBatch).ConfigureAwait(false);
      foreach (KeyValuePair<string, bool> kv in batchResult)
      {
        ret[kv.Key] = kv.Value;
      }
    }
    return ret;
  }

  public async Task UploadObjects(
    string streamId,
    IReadOnlyList<(string, string)> objects,
    Action<ProgressArgs>? progress
  )
  {
    if (objects.Count == 0)
    {
      return;
    }

    // 1. Split into parts of MAX_MULTIPART_SIZE size (can be exceptions until a max of MAX_OBJECT_SIZE if a single obj is larger than MAX_MULTIPART_SIZE)
    List<List<(string, string)>> multipartedObjects = new();
    List<int> multipartedObjectsSize = new();

    List<(string, string)> crtMultipart = new();
    int crtMultipartSize = 0;

    foreach ((string id, string json) in objects)
    {
      int objSize = Encoding.UTF8.GetByteCount(json);
      if (objSize > MAX_OBJECT_SIZE)
      {
        throw new ArgumentException(
          $"Object {id} too large (size {objSize}, max size {MAX_OBJECT_SIZE}). Consider using detached/chunked properties",
          nameof(objects)
        );
      }

      if (crtMultipartSize + objSize <= MAX_MULTIPART_SIZE)
      {
        crtMultipart.Add((id, json));
        crtMultipartSize += objSize;
        continue;
      }

      // new multipart
      if (crtMultipart.Count > 0)
      {
        multipartedObjects.Add(crtMultipart);
        multipartedObjectsSize.Add(crtMultipartSize);
      }
      crtMultipart = new List<(string, string)> { (id, json) };
      crtMultipartSize = objSize;
    }
    multipartedObjects.Add(crtMultipart);
    multipartedObjectsSize.Add(crtMultipartSize);

    // 2. Split multiparts into individual server requests of max size MAX_REQUEST_SIZE or max length MAX_MULTIPART_COUNT and send them
    List<List<(string, string)>> crtRequest = new();
    int crtRequestSize = 0;
    for (int i = 0; i < multipartedObjects.Count; i++)
    {
      List<(string, string)> multipart = multipartedObjects[i];
      int multipartSize = multipartedObjectsSize[i];
      if (crtRequestSize + multipartSize > MAX_REQUEST_SIZE || crtRequest.Count >= MAX_MULTIPART_COUNT)
      {
        await UploadObjectsImpl(streamId, crtRequest, progress).ConfigureAwait(false);
        crtRequest = new List<List<(string, string)>>();
        crtRequestSize = 0;
      }
      crtRequest.Add(multipart);
      crtRequestSize += multipartSize;
    }
    if (crtRequest.Count > 0)
    {
      await UploadObjectsImpl(streamId, crtRequest, progress).ConfigureAwait(false);
    }
  }

  public async Task UploadBlobs(
    string streamId,
    IReadOnlyList<(string, string)> objects,
    Action<ProgressArgs>? progress
  )
  {
    CancellationToken.ThrowIfCancellationRequested();
    if (objects.Count == 0)
    {
      return;
    }

    var multipartFormDataContent = new MultipartFormDataContent();
    var streams = new List<Stream>();
    foreach (var (id, filePath) in objects)
    {
      var fileName = Path.GetFileName(filePath);
      var stream = File.OpenRead(filePath);
      streams.Add(stream);
      StreamContent fsc = new(stream);
      var hash = id.Split(':')[1];

      multipartFormDataContent.Add(fsc, $"hash:{hash}", fileName);
    }

    using var message = new HttpRequestMessage
    {
      RequestUri = new Uri($"/api/stream/{streamId}/blob", UriKind.Relative),
      Method = HttpMethod.Post,
      Content = new ProgressContent(multipartFormDataContent, progress)
    };

    try
    {
      HttpResponseMessage response = await _client.SendAsync(message, CancellationToken).ConfigureAwait(false);

      response.EnsureSuccessStatusCode();

      foreach (var stream in streams)
      {
        stream.Dispose();
      }
    }
    finally
    {
      foreach (var stream in streams)
      {
        stream.Dispose();
      }
    }
  }

  public async Task DownloadBlobs(string streamId, IReadOnlyList<string> blobIds, Action<ProgressArgs>? progress)
  {
    foreach (var blobId in blobIds)
    {
      try
      {
        using var blobMessage = new HttpRequestMessage
        {
          RequestUri = new Uri($"api/stream/{streamId}/blob/{blobId}", UriKind.Relative),
          Method = HttpMethod.Get
        };

        using var response = await _client.SendAsync(blobMessage, CancellationToken).ConfigureAwait(false);
        response.Content.Headers.TryGetValues("Content-Disposition", out IEnumerable<string>? cdHeaderValues);

        var cdHeader = cdHeaderValues?.FirstOrDefault();
        string? fileName = cdHeader?.Split(s_filenameSeparator, StringSplitOptions.None)[1].TrimStart('"').TrimEnd('"');

        string fileLocation = Path.Combine(
          BlobStorageFolder,
          $"{blobId.Substring(0, Blob.LocalHashPrefixLength)}-{fileName}"
        );
        using var source = new ProgressStream(
          await response.Content.ReadAsStreamAsync(),
          response.Content.Headers.ContentLength,
          progress,
          true
        );
        using var fs = new FileStream(fileLocation, FileMode.OpenOrCreate);
        await source.CopyToAsync(fs).ConfigureAwait(false);
      }
      catch (Exception ex) when (!ex.IsFatal())
      {
        throw new SpeckleException($"Failed to download blob {blobId}", ex);
      }
    }
  }

  private async Task DownloadObjectsImpl(
    string streamId,
    IReadOnlyList<string> objectIds,
    Action<ProgressArgs>? progress,
    CbObjectDownloaded onObjectCallback
  )
  {
    // Stopwatch sw = new Stopwatch(); sw.Start();

    CancellationToken.ThrowIfCancellationRequested();

    using var childrenHttpMessage = new HttpRequestMessage
    {
      RequestUri = new Uri($"/api/getobjects/{streamId}", UriKind.Relative),
      Method = HttpMethod.Post
    };

    Dictionary<string, string> postParameters = new() { { "objects", JsonConvert.SerializeObject(objectIds) } };
    string serializedPayload = JsonConvert.SerializeObject(postParameters);
    childrenHttpMessage.Content = new StringContent(serializedPayload, Encoding.UTF8, "application/json");
    childrenHttpMessage.Headers.Add("Accept", "text/plain");

    HttpResponseMessage childrenHttpResponse = await _client
      .SendAsync(childrenHttpMessage, CancellationToken)
      .ConfigureAwait(false);

    await ResponseProgress(childrenHttpResponse, progress, onObjectCallback, false).ConfigureAwait(false);
  }

  private async Task ResponseProgress(
    HttpResponseMessage childrenHttpResponse,
    Action<ProgressArgs>? progress,
    CbObjectDownloaded onObjectCallback,
    bool isSingle
  )
  {
    childrenHttpResponse.EnsureSuccessStatusCode();
    var length = childrenHttpResponse.Content.Headers.ContentLength;
    using Stream childrenStream = await childrenHttpResponse.Content.ReadAsStreamAsync().ConfigureAwait(false);

    using var reader = new StreamReader(new ProgressStream(childrenStream, length, progress, true), Encoding.UTF8);
    while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
    {
      CancellationToken.ThrowIfCancellationRequested();

      if (!isSingle)
      {
        var pcs = line.Split(s_separator, 2);
        onObjectCallback(pcs[0], pcs[1]);
      }
      else
      {
        onObjectCallback(string.Empty, line);
        break;
      }
    }
  }

  private async Task<Dictionary<string, bool>> HasObjectsImpl(string streamId, IReadOnlyList<string> objectIds)
  {
    CancellationToken.ThrowIfCancellationRequested();

    // Stopwatch sw = new Stopwatch(); sw.Start();

    string objectsPostParameter = JsonConvert.SerializeObject(objectIds);
    var payload = new Dictionary<string, string> { { "objects", objectsPostParameter } };
    string serializedPayload = JsonConvert.SerializeObject(payload);
    var uri = new Uri($"/api/diff/{streamId}", UriKind.Relative);

    using StringContent stringContent = new(serializedPayload, Encoding.UTF8, "application/json");
    HttpResponseMessage response = await _client.PostAsync(uri, stringContent, CancellationToken).ConfigureAwait(false);

    response.EnsureSuccessStatusCode();

    var hasObjectsJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
    Dictionary<string, bool> hasObjects = new();

    JObject doc = JObject.Parse(hasObjectsJson);
    foreach (KeyValuePair<string, JToken?> prop in doc)
    {
      hasObjects[prop.Key] = (bool)prop.Value.NotNull();
    }

    // Console.WriteLine($"ServerApi::HasObjects({objectIds.Count}) request in {sw.ElapsedMilliseconds / 1000.0} sec");

    return hasObjects;
  }

  private async Task UploadObjectsImpl(
    string streamId,
    List<List<(string, string)>> multipartedObjects,
    Action<ProgressArgs>? progress
  )
  {
    CancellationToken.ThrowIfCancellationRequested();

    using HttpRequestMessage message =
      new() { RequestUri = new Uri($"/objects/{streamId}", UriKind.Relative), Method = HttpMethod.Post };

    MultipartFormDataContent multipart = new();

    int mpId = 0;
    foreach (List<(string, string)> mpData in multipartedObjects)
    {
      mpId++;

      var ctBuilder = new StringBuilder("[");
      for (int i = 0; i < mpData.Count; i++)
      {
        if (i > 0)
        {
          ctBuilder.Append(',');
        }

        ctBuilder.Append(mpData[i].Item2);
      }
      ctBuilder.Append(']');
      string ct = ctBuilder.ToString();

      if (CompressPayloads)
      {
        var content = new GzipContent(new StringContent(ct, Encoding.UTF8));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/gzip");
        multipart.Add(content, $"batch-{mpId}", $"batch-{mpId}");
      }
      else
      {
        multipart.Add(new StringContent(ct, Encoding.UTF8), $"batch-{mpId}", $"batch-{mpId}");
      }
    }
    message.Content = new ProgressContent(multipart, progress);
    HttpResponseMessage response = await _client.SendAsync(message, CancellationToken).ConfigureAwait(false);

    response.EnsureSuccessStatusCode();
  }

  public async Task<List<string>> HasBlobs(string streamId, IReadOnlyList<string> blobIds)
  {
    CancellationToken.ThrowIfCancellationRequested();

    var payload = JsonConvert.SerializeObject(blobIds);
    var uri = new Uri($"/api/stream/{streamId}/blob/diff", UriKind.Relative);

    using StringContent stringContent = new(payload, Encoding.UTF8, "application/json");

    HttpResponseMessage response = await _client.PostAsync(uri, stringContent, CancellationToken).ConfigureAwait(false);

    response.EnsureSuccessStatusCode();

    var responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
    var parsed = JsonConvert.DeserializeObject<List<string>>(responseString);
    if (parsed is null)
    {
      throw new SpeckleException($"Failed to deserialize successful response {response.Content}");
    }

    return parsed;
  }
}
