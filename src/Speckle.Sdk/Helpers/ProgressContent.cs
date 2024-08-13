using System.Net;
using System.Net.Http.Headers;

namespace Speckle.Sdk.Helpers;

public static class HttpHeaderExtensions
{
  public static void CopyTo(this HttpContentHeaders source, HttpContentHeaders destination)
  {
    foreach (var header in source)
    {
      destination.Add(header.Key, header.Value);
    }
  }
}
internal class ProgressContent : HttpContent
{
  private readonly HttpContent _innerContent;
  private readonly ProgressMessageHandler _handler;
  private readonly HttpRequestMessage _request;

  public ProgressContent(HttpContent innerContent, ProgressMessageHandler handler, HttpRequestMessage request)
  {
    _innerContent = innerContent;
    _handler = handler;
    _request = request;

    innerContent.Headers.CopyTo(Headers);
  }

  protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
  {
    ProgressStream progressStream = new ProgressStream(stream, _handler, _request, response: null);
    return _innerContent.CopyToAsync(progressStream);
  }

  protected override bool TryComputeLength(out long length)
  {
    long? contentLength = _innerContent.Headers.ContentLength;
    if (contentLength.HasValue)
    {
      length = contentLength.Value;
      return true;
    }

    length = -1;
    return false;
  }

  protected override void Dispose(bool disposing)
  {
    base.Dispose(disposing);
    if (disposing)
    {
      _innerContent.Dispose();
    }
  }
}
