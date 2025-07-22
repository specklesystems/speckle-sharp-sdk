using Microsoft.Extensions.Logging;
using Speckle.InterfaceGenerator;
using Speckle.Sdk.Logging;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Caching;

[GenerateAutoInterface]
public class ModelCacheManager(ILogger<ModelCacheManager> logger, IFileSystem fileSystem) : IModelCacheManager
{
  private const string DATA_FOLDER = "Projects";
  private static readonly string s_basePath = SpecklePathProvider.UserSpeckleFolderPath;

  private static string CacheFolder => Path.Combine(s_basePath, DATA_FOLDER);

  public string GetStreamPath(string streamId) => GetDbPath(streamId);

  public static string GetDbPath(string streamId)
  {
    var db = Path.Combine(CacheFolder, $"{streamId}.db");
    try
    {
      Directory.CreateDirectory(CacheFolder); //ensure dir is there
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
          logger.LogWarning(ex, "Failed to delete cache file {filePath}", db);
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
