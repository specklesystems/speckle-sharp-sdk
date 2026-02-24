using System.Diagnostics.CodeAnalysis;

namespace Speckle.Sdk.Pipelines.Progress;

/// <summary>
/// Wraps <paramref name="innerStream"/> to report streaming progress as bytes are read/written.
/// </summary>
public sealed class ProgressStream(Stream innerStream, IProgress<StreamProgressArgs>? progress = null) : Stream
{
  private long _bytesStreamed;

  public override bool CanRead => innerStream.CanRead;
  public override bool CanSeek => innerStream.CanSeek;
  public override bool CanWrite => innerStream.CanWrite;
  public override long Length => innerStream.Length;

  public override long Position
  {
    get => innerStream.Position;
    set => innerStream.Position = value;
  }

  public override int Read(byte[] buffer, int offset, int count)
  {
    int bytesRead = innerStream.Read(buffer, offset, count);
    ReportProgress(bytesRead);
    return bytesRead;
  }

  [SuppressMessage(
    "Performance",
    "CA1835:Prefer the \'Memory\'-based overloads for \'ReadAsync\' and \'WriteAsync\'",
    Justification = "Analyser warning forwarded to caller"
  )]
  public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
  {
    int bytesRead = await innerStream.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
    ReportProgress(bytesRead);
    return bytesRead;
  }

#if NET8_0_OR_GREATER
  public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
  {
    int bytesRead = await innerStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
    ReportProgress(bytesRead);
    return bytesRead;
  }
#endif

  private void ReportProgress(int newBytesProcessed)
  {
    _bytesStreamed += newBytesProcessed;
    progress?.Report(new(_bytesStreamed, Length));
  }

  public override void Flush() => innerStream.Flush();

  public override Task FlushAsync(CancellationToken cancellationToken) => innerStream.FlushAsync(cancellationToken);

  public override long Seek(long offset, SeekOrigin origin) => innerStream.Seek(offset, origin);

  public override void SetLength(long value) => throw new NotSupportedException(); //intentionally not supporting, as changing length of stream mid-flight will fuck up progress

  public override void Write(byte[] buffer, int offset, int count)
  {
    innerStream.Write(buffer, offset, count);
    ReportProgress(count);
  }

  [SuppressMessage(
    "Performance",
    "CA1835:Prefer the \'Memory\'-based overloads for \'ReadAsync\' and \'WriteAsync\'",
    Justification = "Analyser warning forwarded to caller"
  )]
  public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
  {
    await innerStream.WriteAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
    ReportProgress(count);
  }

#if NET6_0_OR_GREATER
  public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
  {
    await innerStream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
    ReportProgress(buffer.Length);
  }
#endif

  protected override void Dispose(bool disposing)
  {
    innerStream.Dispose();
    base.Dispose(disposing);
  }

#if NET6_0_OR_GREATER
  public override async ValueTask DisposeAsync()
  {
    await innerStream.DisposeAsync().ConfigureAwait(false);
    await base.DisposeAsync().ConfigureAwait(false);
  }
#endif
}
