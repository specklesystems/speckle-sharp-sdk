using System.IO.Compression;
using System.Net.Http.Headers;
using System.Threading.Channels;

namespace Speckle.Sdk.Pipeline;

public sealed class Uploader : IDisposable
{
  private readonly string _projectId;
  private readonly string _modelId;
  private readonly HttpClient _client;
  private readonly Channel<UploadItem> _channel;
  private readonly Task _sendTask;

  public Uploader(string projectId, string modelId, string? authToken, string? apiEndpoint)
  {
    _projectId = projectId;
    _modelId = modelId;

    Uri apiBaseUrl = !string.IsNullOrEmpty(apiEndpoint)
      ? new Uri(apiEndpoint)
      : new Uri("http://dimitries-macbook-pro.mermaid-emperor.ts.net/api/v1/");
    // : new Uri("http://zog.local:3000/api/v1/");
    _client = new HttpClient { BaseAddress = apiBaseUrl, Timeout = TimeSpan.FromMinutes(10) };

    if (authToken != null)
    {
      _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
    }

    _channel = Channel.CreateBounded<UploadItem>(
      new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.Wait } // if we're not able to write fast enough, we'll block writes
    );

    _sendTask = SendLoopAsync(projectId, "test");
  }

  public ValueTask PushAsync(UploadItem item, CancellationToken ct = default) => _channel.Writer.WriteAsync(item, ct);

  public async Task CompleteAsync()
  {
    _channel.Writer.Complete();
    await _sendTask.ConfigureAwait(false);
  }

  private async Task SendLoopAsync(string projectId, string modelId)
  {
    var content = new PushStreamContent(
      async (stream, _, _) =>
      {
        var gzip = new GZipStream(stream, CompressionLevel.Optimal);
        var writer = new StreamWriter(gzip); // new StreamWriter(gzip, System.Text.Encoding.UTF8, 20 * 1024 * 1024); // potential lever for controlling memory pressure
        try
        {
          // extra levers for memory pressure in here: we can manually flush every x items or every x bytes
          await foreach (var item in _channel.Reader.ReadAllAsync().ConfigureAwait(false))
          {
            await writer.WriteLineAsync($"{item.Id}\t{item.Json}\t{item.SpeckleType}").ConfigureAwait(false);
          }
        }
        finally
        {
          await writer.FlushAsync().ConfigureAwait(false);
          await gzip.FlushAsync().ConfigureAwait(false);
          writer.Dispose();
          gzip.Dispose();
        }
      },
      new MediaTypeHeaderValue("application/x-ndjson")
    );

    var uri = new Uri($"projects/{projectId}/models/{modelId}/versions", UriKind.Relative);
    var request = new HttpRequestMessage(HttpMethod.Post, uri) { Content = content };
    request.Headers.TransferEncodingChunked = true; // NOTE: important for streaming to happen.
    var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
    response.EnsureSuccessStatusCode();

    // Consume the response body to fully complete the request
    await response.Content.ReadAsStringAsync().ConfigureAwait(false);
  }

  public void Dispose() => _client.Dispose();
}
