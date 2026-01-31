namespace Speckle.Sdk.Pipelines;

/// <summary>
/// Wraps a stream to report upload progress as bytes are read.
/// </summary>
public sealed class ProgressStream : Stream
{
  private readonly Stream _innerStream;
  private readonly long _totalBytes;
  private readonly IProgress<(long BytesSent, long TotalBytes)>? _progress;
  private long _bytesSent;

  public ProgressStream(
    Stream innerStream,
    long totalBytes,
    IProgress<(long BytesSent, long TotalBytes)>? progress = null
  )
  {
    _innerStream = innerStream;
    _totalBytes = totalBytes;
    _progress = progress;
    _bytesSent = 0;
  }

  public override bool CanRead => _innerStream.CanRead;
  public override bool CanSeek => _innerStream.CanSeek;
  public override bool CanWrite => false;
  public override long Length => _innerStream.Length;

  public override long Position
  {
    get => _innerStream.Position;
    set => _innerStream.Position = value;
  }

  public override int Read(byte[] buffer, int offset, int count)
  {
    int bytesRead = _innerStream.Read(buffer, offset, count);
    ReportProgress(bytesRead);
    return bytesRead;
  }

  public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
  {
    int bytesRead = await _innerStream
#if NET8_0_OR_GREATER
      .ReadAsync(buffer.AsMemory(offset, count), cancellationToken)
#else
    .ReadAsync(buffer, offset, count, cancellationToken)
#endif
      .ConfigureAwait(false);
    ReportProgress(bytesRead);
    return bytesRead;
  }

#if NET8_0_OR_GREATER
  public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
  {
    int bytesRead = await _innerStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
    ReportProgress(bytesRead);
    return bytesRead;
  }
#endif

  private void ReportProgress(int bytesRead)
  {
    _bytesSent += bytesRead;
    _progress?.Report((_bytesSent, _totalBytes));
  }

  public override void Flush() => _innerStream.Flush();

  public override Task FlushAsync(CancellationToken cancellationToken) => _innerStream.FlushAsync(cancellationToken);

  public override long Seek(long offset, SeekOrigin origin) => _innerStream.Seek(offset, origin);

  public override void SetLength(long value) => throw new NotSupportedException();

  public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

  protected override void Dispose(bool disposing)
  {
    if (disposing)
    {
      _innerStream.Dispose();
    }
    base.Dispose(disposing);
  }

#if NET8_0_OR_GREATER
  public override async ValueTask DisposeAsync()
  {
    await _innerStream.DisposeAsync().ConfigureAwait(false);
    await base.DisposeAsync().ConfigureAwait(false);
  }
#endif
}
