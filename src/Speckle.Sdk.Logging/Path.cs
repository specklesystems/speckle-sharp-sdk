using System.Reflection;

namespace Speckle.Sdk.Logging;

/// <summary>
/// Helper class dedicated for Speckle specific Path operations.
/// </summary>
public static class SpecklePathProvider
{
  private static string s_applicationName = "Speckle";

  private static string s_blobFolderName = "Blobs";

  private static string s_kitsFolderName = "Kits";

  private static string s_accountsFolderName = "Accounts";

  private static string s_objectsFolderName = "Objects";

  private const string LOG_FOLDER_NAME = "Logs";

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
  /// Get the path where the Speckle applications should be installed
  /// </summary>
  public static string InstallSpeckleFolderPath => EnsureFolderExists(InstallApplicationDataPath, s_applicationName);

  /// <summary>
  /// Get the folder where the user's Speckle data should be stored.
  /// </summary>
  public static string UserSpeckleFolderPath => EnsureFolderExists(UserApplicationDataPath(), s_applicationName);

  /// <summary>
  /// Get the folder where the Speckle kits should be stored.
  /// </summary>
  public static string KitsFolderPath => EnsureFolderExists(InstallSpeckleFolderPath, s_kitsFolderName);

  /// <summary>
  ///
  /// </summary>
  public static string ObjectsFolderPath => EnsureFolderExists(KitsFolderPath, s_objectsFolderName);

  /// <summary>
  /// Get the folder where the Speckle accounts data should be stored.
  /// </summary>
  public static string AccountsFolderPath => EnsureFolderExists(UserSpeckleFolderPath, s_accountsFolderName);

  /// <summary>
  /// Override the global Speckle application name.
  /// </summary>
  /// <param name="applicationName"></param>
  public static void OverrideApplicationName(string applicationName)
  {
    s_applicationName = applicationName;
  }

  /// <summary>
  /// Override the global Speckle application data path.
  /// </summary>
  public static void OverrideApplicationDataPath(string? path)
  {
    Environment.SetEnvironmentVariable(UserDataPathEnvVar, path);
  }

  /// <summary>
  /// Override the global Blob storage folder name.
  /// </summary>
  public static void OverrideBlobStorageFolder(string blobFolderName)
  {
    s_blobFolderName = blobFolderName;
  }

  /// <summary>
  ///  Override the global Kits folder name.
  /// </summary>
  public static void OverrideKitsFolderName(string kitsFolderName)
  {
    s_kitsFolderName = kitsFolderName;
  }

  /// <summary>
  /// Override the global Accounts folder name.
  /// </summary>
  public static void OverrideAccountsFolderName(string accountsFolderName)
  {
    s_accountsFolderName = accountsFolderName;
  }

  /// <summary>
  ///
  /// </summary>
  public static void OverrideObjectsFolderName(string objectsFolderName)
  {
    s_objectsFolderName = objectsFolderName;
  }

  /// <summary>
  /// Get the platform specific user configuration folder path.<br/>
  /// Unless overriden (see <see cref="OverrideApplicationDataPath"/>) will be the <see cref="Environment.SpecialFolder.ApplicationData"/> path e.g.:
  /// In cases such as linux servers where the above path is not permissive, we will fall back to <see cref="Environment.SpecialFolder.UserProfile"/>
  /// </summary>
  /// <remarks>
  /// <see cref="Environment.SpecialFolder.ApplicationData"/> path usually maps to
  /// <ul>
  ///   <li>win: <c>%appdata%/</c></li>
  ///   <li>MacOS: <c>~/.config/</c></li>
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

    // on desktop linux and macos we use the appdata.
    // but we might not have write access to the disk
    // so the catch falls back to the user profile
    try
    {
      return Environment.GetFolderPath(
        Environment.SpecialFolder.ApplicationData,
        // if the folder doesn't exist, we get back an empty string on OSX,
        // which in turn, breaks other stuff down the line.
        // passing in the Create option ensures that this directory exists,
        // which is not a given on all OS-es.
        Environment.SpecialFolderOption.Create
      );
    }
    catch (SystemException ex) when (ex is PlatformNotSupportedException or ArgumentException)
    {
      //Adding this log just so we confidently know which Exception type to catch here.
      // TODO: Must re-add log call when (and if) this get's made as a service
      //SpeckleLog.Logger.Warning(ex, "Falling back to user profile path");

      // on server linux, there might not be a user setup, things can run under root
      // in that case, the appdata variable is most probably not set up
      // we fall back to the value of the home folder
      return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }
  }

  /// <summary>
  /// Get the folder where the user's Speckle blobs should be stored.
  /// </summary>
  public static string BlobStoragePath(string? path = null)
  {
    return EnsureFolderExists(path ?? UserSpeckleFolderPath, s_blobFolderName);
  }

  private static string EnsureFolderExists(params string[] folderName)
  {
    var path = System.IO.Path.Combine(folderName);
    Directory.CreateDirectory(path);
    return path;
  }

  internal static string LogFolderPath(string applicationAndVersion) =>
    EnsureFolderExists(UserSpeckleFolderPath, LOG_FOLDER_NAME, applicationAndVersion);
}
