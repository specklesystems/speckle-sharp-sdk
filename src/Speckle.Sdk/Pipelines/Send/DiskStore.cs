using System.IO.Compression;
using System.Text;
using Microsoft.Extensions.Logging;
using Speckle.InterfaceGenerator;
using Speckle.Sdk.Dependencies;
using Speckle.Sdk.Helpers;
using Speckle.Sdk.Logging;

namespace Speckle.Sdk.Pipelines.Send;

[GenerateAutoInterface]
public sealed class DiskStoreFactory(ILogger<DiskStore> logger, ISdkActivityFactory activityFactory) : IDiskStoreFactory
{
  public DiskStore CreateInstance(CancellationToken cancellationToken) =>
    new(logger, activityFactory, cancellationToken);
}

public sealed class DiskStore
{
  private readonly RepackedChannel<UploadItem> _channel;
  private readonly Task<DisposableFile> _writeToDiskTask;
  private readonly ILogger<DiskStore> _logger;
  private readonly ISdkActivityFactory _activityFactory;
  private readonly CancellationToken _cancellationToken;

  internal DiskStore(
    ILogger<DiskStore> logger,
    ISdkActivityFactory activityFactory,
    CancellationToken cancellationToken
  )
  {
    _logger = logger;
    _activityFactory = activityFactory;
    _cancellationToken = cancellationToken;

    _channel = new RepackedChannel<UploadItem>(1000, true, false);
    _writeToDiskTask = Task.Run(WriteFile, cancellationToken);
  }

  public async Task PushAsync(UploadItem item) =>
    await _channel.WriteAsync(item, _cancellationToken).ConfigureAwait(false);

  public async Task<DisposableFile> CompleteAsync()
  {
    using var a = _activityFactory.Start("Waiting for DiskStore to complete");
    _channel.CompleteWriter();
    return await _writeToDiskTask.ConfigureAwait(false);
  }

  private readonly byte[] _newLineChar = [(byte)'\n'];
  private readonly byte[] _tabLineChar = [(byte)'\t'];

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

      await foreach (var item in _channel.ReadAllAsync(_cancellationToken).ConfigureAwait(false))
      {
        byte[] id = Encoding.UTF8.GetBytes(item.Id);
        byte[] speckleType = Encoding.UTF8.GetBytes(item.SpeckleType);
#if NET5_0_OR_GREATER
        await gzip.WriteAsync(id, _cancellationToken).ConfigureAwait(false);
        await gzip.WriteAsync(_tabLineChar, _cancellationToken).ConfigureAwait(false);
        await gzip.WriteAsync(speckleType, _cancellationToken).ConfigureAwait(false);
        await gzip.WriteAsync(_tabLineChar, _cancellationToken).ConfigureAwait(false);
        await gzip.WriteAsync(item.Json.WrittenMemory, _cancellationToken).ConfigureAwait(false);
        await gzip.WriteAsync(_newLineChar, _cancellationToken).ConfigureAwait(false);
#else
        await gzip.WriteAsync(id, 0, id.Length, _cancellationToken).ConfigureAwait(false);
        await gzip.WriteAsync(_tabLineChar, 0, _tabLineChar.Length, _cancellationToken).ConfigureAwait(false);
        await gzip.WriteAsync(speckleType, 0, speckleType.Length, _cancellationToken).ConfigureAwait(false);
        await gzip.WriteAsync(_tabLineChar, 0, _tabLineChar.Length, _cancellationToken).ConfigureAwait(false);
        await gzip.WriteAsync(item.Json.GetInternalBuffer(), 0, item.Json.WrittenCount, _cancellationToken)
          .ConfigureAwait(false);
        await gzip.WriteAsync(_newLineChar, 0, _newLineChar.Length, _cancellationToken).ConfigureAwait(false);
#endif
        item.Dispose();
      }

      await gzip.FlushAsync(_cancellationToken).ConfigureAwait(false);

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
