using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using Speckle.InterfaceGenerator;
using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Common;
using Speckle.Sdk.Dependencies.Serialization;
using Speckle.Sdk.Helpers;
using Speckle.Sdk.Logging;
using Speckle.Sdk.Transports;
using Speckle.Sdk.Transports.ServerUtils;

namespace Speckle.Sdk.Serialisation.V2;

[GenerateAutoInterface]
public class ServerObjectManager : IServerObjectManager
{
  private static readonly char[] s_separator = { '\t' };

  private readonly ISdkActivityFactory _activityFactory;
  private readonly HttpClient _client;

  public ServerObjectManager(
    ISpeckleHttp speckleHttp,
    ISdkActivityFactory activityFactory,
    Uri baseUri,
    string? authorizationToken,
    int timeoutSeconds = 120
  )
  {
    _activityFactory = activityFactory;
    _client = speckleHttp.CreateHttpClient(
      new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip },
      timeoutSeconds: timeoutSeconds,
      authorizationToken: authorizationToken
    );
    _client.BaseAddress = baseUri;
  }

  public async IAsyncEnumerable<(string, string)> DownloadObjects(
    string streamId,
    IReadOnlyList<string> objectIds,
    IProgress<ProgressArgs>? progress,
    [EnumeratorCancellation] CancellationToken cancellationToken
  )
  {
    using var _ = _activityFactory.Start();
    cancellationToken.ThrowIfCancellationRequested();

    using var childrenHttpMessage = new HttpRequestMessage
    {
      RequestUri = new Uri($"/api/getobjects/{streamId}", UriKind.Relative),
      Method = HttpMethod.Post,
    };

    Dictionary<string, string> postParameters = new() { { "objects", JsonConvert.SerializeObject(objectIds) } };
    string serializedPayload = JsonConvert.SerializeObject(postParameters);
    childrenHttpMessage.Content = new StringContent(serializedPayload, Encoding.UTF8, "application/json");
    childrenHttpMessage.Headers.Add("Accept", "text/plain");

    HttpResponseMessage childrenHttpResponse = await _client
      .SendAsync(childrenHttpMessage, cancellationToken)
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
    string streamId,
    string objectId,
    IProgress<ProgressArgs>? progress,
    CancellationToken cancellationToken
  )
  {
    using var _ = _activityFactory.Start();
    cancellationToken.ThrowIfCancellationRequested();

    // Get root object
    using var rootHttpMessage = new HttpRequestMessage
    {
      RequestUri = new Uri($"/objects/{streamId}/{objectId}/single", UriKind.Relative),
      Method = HttpMethod.Get,
    };

    HttpResponseMessage rootHttpResponse = await _client
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
#if NET8_0_OR_GREATER
    using Stream childrenStream = await childrenHttpResponse
      .Content.ReadAsStreamAsync(cancellationToken)
      .ConfigureAwait(false);
#else
    using Stream childrenStream = await childrenHttpResponse.Content.ReadAsStreamAsync().ConfigureAwait(false);
#endif

    using var reader = new StreamReader(new ProgressStream(childrenStream, length, progress, true), Encoding.UTF8);

#if NET8_0_OR_GREATER
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
    string streamId,
    IReadOnlyList<string> objectIds,
    CancellationToken cancellationToken
  )
  {
    cancellationToken.ThrowIfCancellationRequested();

    // Stopwatch sw = new Stopwatch(); sw.Start();

    string objectsPostParameter = JsonConvert.SerializeObject(objectIds);
    var payload = new Dictionary<string, string> { { "objects", objectsPostParameter } };
    string serializedPayload = JsonConvert.SerializeObject(payload);
    var uri = new Uri($"/api/diff/{streamId}", UriKind.Relative);

    using StringContent stringContent = new(serializedPayload, Encoding.UTF8, "application/json");
    using HttpResponseMessage response = await _client
      .PostAsync(uri, stringContent, cancellationToken)
      .ConfigureAwait(false);

    response.EnsureSuccessStatusCode();
#if NET8_0_OR_GREATER
    var hasObjects = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
    var hasObjects = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
    return JsonConvert.DeserializeObject<Dictionary<string, bool>>(hasObjects).NotNull();
  }

  public async Task UploadObjects(
    string streamId,
    IReadOnlyList<BaseItem> objects,
    bool compressPayloads,
    IProgress<ProgressArgs>? progress,
    CancellationToken cancellationToken
  )
  {
    cancellationToken.ThrowIfCancellationRequested();

    using HttpRequestMessage message =
      new() { RequestUri = new Uri($"/objects/{streamId}", UriKind.Relative), Method = HttpMethod.Post };

    MultipartFormDataContent multipart = new();

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
    HttpResponseMessage response = await _client.SendAsync(message, cancellationToken).ConfigureAwait(false);

    response.EnsureSuccessStatusCode();
  }
}
