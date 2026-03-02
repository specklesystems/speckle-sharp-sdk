using System.IO.Compression;
using Microsoft.Extensions.Logging;
using Speckle.InterfaceGenerator;
using Speckle.Sdk.Dependencies;
using Speckle.Sdk.Helpers;

namespace Speckle.Sdk.Pipelines.Send;

[GenerateAutoInterface]
public sealed class DiskStoreFactory(ILogger<DiskStore> logger) : IDiskStoreFactory
{
  public DiskStore CreateInstance(CancellationToken cancellationToken) => new(logger, cancellationToken);
}

public sealed class DiskStore
{
  private readonly RepackedChannel<UploadItem> _channel;
  private readonly Task<DisposableFile> _writeToDiskTask;
  private readonly ILogger<DiskStore> _logger;
  private readonly CancellationToken _cancellationToken;

  internal DiskStore(ILogger<DiskStore> logger, CancellationToken cancellationToken)
  {
    _logger = logger;
    _cancellationToken = cancellationToken;

    _channel = new RepackedChannel<UploadItem>(1000, true, false);
    _writeToDiskTask = Task.Run(WriteFile, cancellationToken);
  }

  public async Task PushAsync(UploadItem item) =>
    await _channel.WriteAsync(item, _cancellationToken).ConfigureAwait(false);

  public async Task<DisposableFile> CompleteAsync()
  {
    _channel.CompleteWriter();
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
    var tempFile = new DisposableFile(new FileInfo(tempFilePath), _logger);
    _logger.LogInformation("Writing temp file to {TempFilePath}", tempFilePath);

    try
    {
      using var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
      using var gzip = new GZipStream(fileStream, CompressionLevel.Optimal);
      using var writer = new StreamWriter(gzip);

      await foreach (var item in _channel.ReadAllAsync(_cancellationToken).ConfigureAwait(false))
      {
        await writer.WriteLineAsync($"{item.Id}\t{item.Json}\t{item.SpeckleType}").ConfigureAwait(false);
      }
#if NET8_0_OR_GREATER
      await writer.FlushAsync(_cancellationToken).ConfigureAwait(false);
#else
      await writer.FlushAsync().ConfigureAwait(false);
#endif
      tempFile.FileInfo.Refresh();

      return tempFile;
    }
    catch
    {
      tempFile.Dispose();
      throw;
    }
  }
}
