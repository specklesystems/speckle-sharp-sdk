using Speckle.Sdk.Transports.ServerUtils;

namespace Speckle.Sdk.Transports;

internal class ProgressStream(Stream input, long? streamLength, Action<ProgressArgs>? progress) : Stream
{
  private long _position;

  public override void Flush() => throw new NotImplementedException();

  public override long Seek(long offset, SeekOrigin origin) => throw new NotImplementedException();

  public override void SetLength(long value) => throw new NotImplementedException();

  public override int Read(byte[] buffer, int offset, int count)
  {
    int n = input.Read(buffer, offset, count);
    _position += n;
    int? percentage = null;
    if (streamLength is not null)
    {
      percentage = Convert.ToInt32(_position / streamLength);
    }
    progress?.Invoke(new (ProgressEvent.DownloadBytes, percentage, _position, streamLength));
    return n;
  }

  public override void Write(byte[] buffer, int offset, int count) => throw new NotImplementedException();

  public override bool CanRead => true;
  public override bool CanSeek => false;
  public override bool CanWrite => false;
  public override long Length => streamLength ?? 0;
  public override long Position
  {
    get => _position;
    set => throw new NotImplementedException();
  }
}
