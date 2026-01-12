using System.IO.Compression;
using System.Net.Http.Headers;
using System.Threading.Channels;

namespace Speckle.Sdk.Serialisation.V2.Send;

#pragma warning disable CA1001
public sealed class ObjectFlopper
#pragma warning restore CA1001
{
  private readonly Uri _url;
  private readonly string _streamId;
  private readonly HttpClient _client;
  private readonly Channel<BaseItem> _channel;
  private readonly Task _sendTask;

  public ObjectFlopper(Uri _, string streamId, string? authToken)
  {
    _streamId = streamId;
    _url = new Uri("http://zog.local:3000/api/v1/");
    _client = new HttpClient { BaseAddress = _url, Timeout = TimeSpan.FromMinutes(10) };

    if (authToken != null)
    {
      _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
    }

    _channel = Channel.CreateBounded<BaseItem>(
      new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.Wait }
    );

    _sendTask = SendLoopAsync(streamId, "test");
  }

  public ValueTask PushAsync(BaseItem item, CancellationToken ct = default) => _channel.Writer.WriteAsync(item, ct);

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
        var writer = new StreamWriter(gzip); //new StreamWriter(gzip, System.Text.Encoding.UTF8, 20 * 1024 * 1024);
        try
        {
          await foreach (var item in _channel.Reader.ReadAllAsync().ConfigureAwait(false))
          {
            await writer.WriteLineAsync($"{item.Id}\t{item.Json}").ConfigureAwait(false);
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
