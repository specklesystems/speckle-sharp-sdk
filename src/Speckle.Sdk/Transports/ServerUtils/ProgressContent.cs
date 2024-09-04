using System.Net;

namespace Speckle.Sdk.Transports.ServerUtils;

internal class ProgressContent : HttpContent
{
  private readonly HttpContent _innerContent;
  private readonly Action<ProgressArgs>? _progress;

  public ProgressContent(HttpContent innerContent, Action<ProgressArgs>? progress)
  {
    _innerContent = innerContent;
    _progress = progress;

    innerContent.Headers.CopyTo(Headers);
  }

  protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
  {
    ProgressStream progressStream = new(stream, _innerContent.Headers.ContentLength, _progress, false);
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
