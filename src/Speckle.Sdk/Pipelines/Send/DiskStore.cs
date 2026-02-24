using System.IO.Compression;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Speckle.InterfaceGenerator;
using Speckle.Sdk.Helpers;

namespace Speckle.Sdk.Pipelines.Send;

[GenerateAutoInterface]
public sealed class DiskStoreFactory(ILogger<DiskStore> logger) : IDiskStoreFactory
{
  public DiskStore CreateInstance(CancellationToken cancellationToken) => new(logger, cancellationToken);
}

public sealed class DiskStore
{
  private readonly Channel<UploadItem> _channel;
  private readonly Task<DisposableFile> _writeToDiskTask;
  private readonly ILogger<DiskStore> _logger;
  private readonly CancellationToken _cancellationToken;

  internal DiskStore(ILogger<DiskStore> logger, CancellationToken cancellationToken)
  {
    _logger = logger;
    _cancellationToken = cancellationToken;

    _channel = Channel.CreateBounded<UploadItem>(
      new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.Wait, SingleReader = true }
    );
    _writeToDiskTask = Task.Run(WriteFile, cancellationToken);
  }

  public ValueTask PushAsync(UploadItem item) => _channel.Writer.WriteAsync(item, _cancellationToken);

  public async Task<DisposableFile> CompleteAsync()
  {
    _channel.Writer.Complete();
    return await _writeToDiskTask.ConfigureAwait(false);
  }

  /// <summary>
  /// Reads from the Channel and streams the <see cref="UploadItem"/>s to a temporary file on disk.
  /// Will keep reading until <see cref="CompleteAsync"/> is called.
  /// </summary>
  /// <returns>the file that was written</returns>
  private async Task<DisposableFile> WriteFile()
  {
    string tempFilePath = Path.GetTempFileName();
    _logger.LogInformation("Writing temp file to {TempFilePath}", tempFilePath);

    using var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
    using var gzip = new GZipStream(fileStream, CompressionLevel.Optimal);
    using var writer = new StreamWriter(gzip);

    await foreach (var item in _channel.Reader.ReadAllAsync(_cancellationToken).ConfigureAwait(false))
    {
      await writer.WriteLineAsync($"{item.Id}\t{item.Json}\t{item.SpeckleType}").ConfigureAwait(false);
    }
#if NET8_0_OR_GREATER
    await writer.FlushAsync(_cancellationToken).ConfigureAwait(false);
#else
    await writer.FlushAsync().ConfigureAwait(false);
#endif
    return new DisposableFile(new FileInfo(tempFilePath), _logger);
  }
}
