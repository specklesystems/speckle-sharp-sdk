using System.IO.Compression;
using System.IO.Pipelines;
using System.Net.Http.Headers;
using System.Threading.Channels;

namespace Speckle.Sdk.Serialisation.V2.Send;

#pragma warning disable CA1001
public sealed class ObjectFlopperGandalf
#pragma warning restore CA1001
{
  private readonly Uri _url;
  private readonly string _streamId;
  private readonly HttpClient _client;
  private readonly Channel<BaseItem> _channel;
  private readonly Task _sendTask;

  public ObjectFlopperGandalf(Uri _, string streamId, string? authToken)
  {
    _streamId = streamId;
    _url = new Uri("http://bender-2.local:3000/api/v1/");
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
    var pipe = new Pipe();

    // Start writing to pipe immediately in background
    var writeTask = Task.Run(async () =>
    {
      var gzip = new GZipStream(pipe.Writer.AsStream(), CompressionLevel.Optimal);
      var writer = new StreamWriter(gzip);

      try
      {
        await foreach (var item in _channel.Reader.ReadAllAsync().ConfigureAwait(false))
        {
          await writer.WriteLineAsync($"{item.Id}\t{item.Json}").ConfigureAwait(false);
          await writer.FlushAsync().ConfigureAwait(false);
        }
      }
      finally
      {
        await writer.FlushAsync().ConfigureAwait(false);
        await gzip.FlushAsync().ConfigureAwait(false);
        writer.Dispose();
        gzip.Dispose();
        await pipe.Writer.CompleteAsync().ConfigureAwait(false);
      }
    });

    // Start HTTP request immediately, reading from pipe
    var content = new StreamContent(pipe.Reader.AsStream());
    content.Headers.ContentType = new MediaTypeHeaderValue("application/x-ndjson");
    content.Headers.ContentEncoding.Add("gzip");

    var uri = new Uri($"projects/{projectId}/models/{modelId}/objects", UriKind.Relative);
    var request = new HttpRequestMessage(HttpMethod.Post, uri) { Content = content };

    var responseTask = _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

    // Wait for both
    await Task.WhenAll(writeTask, responseTask).ConfigureAwait(false);

    var response = await responseTask.ConfigureAwait(false);
    response.EnsureSuccessStatusCode();
  }

  public void Dispose() => _client.Dispose();
}
