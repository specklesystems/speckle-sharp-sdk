namespace Speckle.Sdk.Transports;

internal sealed class ProgressStream(
  Stream input,
  long? streamLength,
  IProgress<ProgressArgs>? progress,
  bool useBuffer
) : Stream
{
  private long _position;
  private readonly Stream _stream = useBuffer ? new BufferedStream(input, 80 * 1024) : input;

  public override void Flush() => _stream.Flush();

  public override long Seek(long offset, SeekOrigin origin) => throw new NotImplementedException();

  public override void SetLength(long value) => throw new NotImplementedException();

  public override int Read(byte[] buffer, int offset, int count)
  {
    int n = _stream.Read(buffer, offset, count);
    _position += n;
    progress?.Report(new(ProgressEvent.DownloadBytes, _position, streamLength));
    return n;
  }

  public override void Write(byte[] buffer, int offset, int count)
  {
    _stream.Write(buffer, offset, count);
    _position += count;
    progress?.Report(new(ProgressEvent.UploadBytes, _position, streamLength));
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
    _stream.Dispose();
    base.Dispose(disposed);
  }
}
