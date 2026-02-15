using Microsoft.Extensions.Logging;

namespace Speckle.Sdk.Helpers;

/// <summary>
/// <see cref="IDisposable"/> wrapper around the downloaded file to try and delete the file on Dispose
/// </summary>
/// <remarks>
/// We're using a similar pattern in the Rhino File Importer codebase (see <c>ImportJobFile</c>)
/// </remarks>
/// <param name="logger"></param>
/// <param name="file"></param>
public sealed class DisposableFile(FileInfo file, ILogger logger, bool deleteOnDispose = true) : IDisposable
{
  public FileInfo FileInfo => file;

  public void Dispose()
  {
    if (!deleteOnDispose)
    {
      return;
    }

    try
    {
      file.Delete();
      logger.LogInformation("Cleaned up {File}", file);
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
      logger.LogWarning(ex, "Failed to clean up {File}", file);
    }
  }
}
