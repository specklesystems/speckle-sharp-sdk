using System.Reflection;

namespace Speckle.Sdk.Logging;

/// <summary>
/// Helper class dedicated for Speckle specific Path operations.
/// </summary>
public static class SpecklePathProvider
{
  private const string APPLICATION_NAME = "Speckle";

  private const string BLOB_FOLDER_NAME = "Blobs";

  private const string ACCOUNTS_FOLDER_NAME = "Accounts";

  private static string UserDataPathEnvVar => "SPECKLE_USERDATA_PATH";
  private static string? Path => Environment.GetEnvironmentVariable(UserDataPathEnvVar);

  /// <summary>
  /// Get the installation path.
  /// </summary>
  public static string InstallApplicationDataPath =>
    Assembly.GetExecutingAssembly().Location.Contains("ProgramData")
      ? Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)
      : UserApplicationDataPath();

  /// <summary>
  /// Get the folder where the user's Speckle data should be stored.
  /// </summary>
  public static string UserSpeckleFolderPath => EnsureFolderExists(UserApplicationDataPath(), APPLICATION_NAME);

  /// <summary>
  /// Get the folder where the Speckle accounts data should be stored.
  /// </summary>
  public static string AccountsFolderPath => EnsureFolderExists(UserSpeckleFolderPath, ACCOUNTS_FOLDER_NAME);

  /// <summary>
  /// Get the platform specific user configuration folder path.<br/>
  /// will be the <see cref="Environment.SpecialFolder.ApplicationData"/> path e.g.:
  /// In cases such as linux servers where the above path is not permissive, we will fall back to <see cref="Environment.SpecialFolder.UserProfile"/>
  /// </summary>
  /// <remarks>
  /// <see cref="Environment.SpecialFolder.ApplicationData"/> path usually maps to
  /// <ul>
  ///   <li>win: <c>%appdata%/</c></li>
  ///   <li>MacOS: <c>~/Library/Application Support</c></li>
  ///   <li>Linux: <c>~/.config/</c></li>
  /// </ul>
  /// </remarks>
  /// <exception cref="PlatformNotSupportedException">Both <see cref="Environment.SpecialFolder.ApplicationData"/> and <see cref="Environment.SpecialFolder.UserProfile"/> paths are inaccessible</exception>
  public static string UserApplicationDataPath()
  {
    // if we have an override, just return that
    var pathOverride = Path;
    if (pathOverride != null && !string.IsNullOrEmpty(pathOverride))
    {
      return pathOverride;
    }

    try
    {
      return Environment.GetFolderPath(
        Environment.SpecialFolder.ApplicationData,
        // It's not a given that the folder is already there on all OS-es, so we'll create it
        Environment.SpecialFolderOption.Create
      );
    }
    catch (PlatformNotSupportedException)
    {
      // We might not have write access to the disk to create the folder,
      // so we'll fall back to the user profile
      return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }
  }

  /// <summary>
  /// Get the folder where the user's Speckle blobs should be stored.
  /// </summary>
  public static string BlobStoragePath(string? path = null) =>
    EnsureFolderExists(path ?? UserSpeckleFolderPath, BLOB_FOLDER_NAME);

  private static string EnsureFolderExists(params string[] folderName)
  {
    var path = System.IO.Path.Combine(folderName);
    Directory.CreateDirectory(path);
    return path;
  }
}
