using Speckle.Sdk.Logging;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Serialisation.Utilities;

public static class SqlitePaths
{
  private const string APPLICATION_NAME = "Speckle";
  private const string DATA_FOLDER = "Projects";
  private static readonly string basePath = SpecklePathProvider.UserApplicationDataPath();

  public static string BlobStorageFolder =>
    SpecklePathProvider.BlobStoragePath(Path.Combine(basePath, APPLICATION_NAME));

  public static string GetDBPath(string streamId)
  {
    var dir = Path.Combine(basePath, APPLICATION_NAME, DATA_FOLDER);
    var db = Path.Combine(dir, $"{streamId}.db");
    try
    {
      Directory.CreateDirectory(dir); //ensure dir is there
      return db;
    }
    catch (Exception ex)
      when (ex is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
    {
      throw new TransportException($"Path was invalid or could not be created {db}", ex);
    }
  }
}
