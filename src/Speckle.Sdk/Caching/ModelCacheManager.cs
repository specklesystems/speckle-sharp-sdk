using Microsoft.Extensions.Logging;
using Speckle.InterfaceGenerator;
using Speckle.Sdk.Logging;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Caching;

[GenerateAutoInterface]
public class ModelCacheManager(ILogger<ModelCacheManager> logger, IFileSystem fileSystem) : IModelCacheManager
{
  private const string APPLICATION_NAME = "Speckle";
  private const string DATA_FOLDER = "Projects";
  private static readonly string basePath = SpecklePathProvider.UserApplicationDataPath();

  public static string BlobStorageFolder =>
    SpecklePathProvider.BlobStoragePath(Path.Combine(basePath, APPLICATION_NAME));

  public static string CacheFolder => Path.Combine(basePath, APPLICATION_NAME, DATA_FOLDER);

  public string GetStreamPath(string streamId) => GetDBPath(streamId);

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

  public void ClearCache()
  {
    try
    {
      if (!fileSystem.DirectoryExists(CacheFolder))
      {
        return;
      }

      foreach (var db in fileSystem.EnumerateFiles(CacheFolder))
      {
        try
        {
          fileSystem.DeleteFile(db);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
          logger.LogWarning(ex, "Failed to delete cache file {a}", db);
        }
      }
    }
    catch (Exception ex)
      when (ex is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
    {
      throw new TransportException($"Cache folder could not be cleared: {CacheFolder}", ex);
    }
  }

  public long GetCacheSize()
  {
    try
    {
      if (!fileSystem.DirectoryExists(CacheFolder))
      {
        return 0;
      }

      long size = 0;
      foreach (var file in fileSystem.EnumerateFiles(CacheFolder))
      {
        try
        {
          size += fileSystem.GetFileSize(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
          logger.LogWarning(ex, "Failed to get size for cache file {a}", file);
        }
      }
      return size;
    }
    catch (Exception ex)
      when (ex is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
    {
      throw new TransportException($"Cache folder size could not be determined: {CacheFolder}", ex);
    }
  }
}
