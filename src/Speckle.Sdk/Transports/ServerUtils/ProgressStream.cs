namespace Speckle.Sdk.Transports;

internal sealed class ProgressStream(Stream input, long? streamLength, Action<ProgressArgs>? progress)
  : Stream
{
  private long _position;

  public override void Flush() => input.Flush();

  public override long Seek(long offset, SeekOrigin origin) => throw new NotImplementedException();

  public override void SetLength(long value) => throw new NotImplementedException();

  public override int Read(byte[] buffer, int offset, int count)
  {
    int n = input.Read(buffer, offset, count);
    _position += n;
    progress?.Invoke(new(ProgressEvent.DownloadBytes, _position, streamLength));
    return n;
  }

  public override void Write(byte[] buffer, int offset, int count)
  {
    input.Write(buffer, offset, count);
    _position += count;
    progress?.Invoke(new(ProgressEvent.UploadBytes, _position, streamLength));
  }

  public override bool CanRead => true;
  public override bool CanSeek => false;
  public override bool CanWrite => true;
  public override long Length => streamLength ?? 0;
  public override long Position
  {
    get => _position;
    set => throw new NotImplementedException();
  }

  protected override void Dispose(bool disposed)
  {
    input.Dispose();
    base.Dispose(disposed);
  }
}
