namespace Speckle.Sdk.Transports;

internal sealed class ProgressStream(
  Stream input,
  long? streamLength,
  Func<ProgressArgs, Task>? progress,
  bool useBuffer
) : Stream
{
  private long _position;
  private readonly Stream _stream = useBuffer ? new BufferedStream(input, 80 * 1024) : input;

  public override void Flush() => _stream.Flush();

  public override long Seek(long offset, SeekOrigin origin) => throw new NotImplementedException();

  public override void SetLength(long value) => throw new NotImplementedException();

#if NETSTANDARD2_0
  public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
  {
    int n = await _stream.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
    _position += n;
    if (progress != null)
    {
      await progress.Invoke(new(ProgressEvent.DownloadBytes, _position, streamLength)).ConfigureAwait(false);
    }
    return n;
  }
#else
  public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
  {
    int n = await _stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);

    return n;
  }

  public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
  {
    int n = await ReadAsync(new Memory<byte>(buffer, offset, count), cancellationToken).ConfigureAwait(false);
    _position += n;
    if (progress != null)
    {
      await progress.Invoke(new(ProgressEvent.DownloadBytes, _position, streamLength)).ConfigureAwait(false);
    }
    return n;
  }
#endif

  public override int Read(byte[] buffer, int offset, int count) => throw new NotImplementedException();

#if NETSTANDARD2_0
  public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
  {
    await _stream.WriteAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
    _position += buffer.Length;
    if (progress != null)
    {
      await progress.Invoke(new(ProgressEvent.UploadBytes, _position, streamLength)).ConfigureAwait(false);
    }
  }
#else
  public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
  {
    await _stream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
    _position += buffer.Length;
    if (progress != null)
    {
      await progress.Invoke(new(ProgressEvent.UploadBytes, _position, streamLength)).ConfigureAwait(false);
    }
  }

  public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
    await WriteAsync(new Memory<byte>(buffer, offset, count), cancellationToken).ConfigureAwait(false);
#endif
  public override void Write(byte[] buffer, int offset, int count) => throw new NotImplementedException();

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
