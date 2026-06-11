using Speckle.Sdk.Common;

namespace Speckle.Sdk.Models;

/// <summary>
/// Helpers that adapt the file-backed <see cref="Blob"/> to in-memory byte buffers
/// without modifying the core Blob type.
/// </summary>
public static class BlobExtensions
{
  /// <summary>
  /// Writes <paramref name="bytes"/> to a temp file under <paramref name="tempDir"/> and
  /// returns a <see cref="Blob"/> backed by it. The transport pipeline hashes the file
  /// and relocates it on send.
  /// </summary>
  public static Blob FromBytes(byte[] bytes, string tempDir, string suffix = ".smsh")
  {
    _ = bytes.NotNull();
    if (string.IsNullOrEmpty(tempDir))
    {
      throw new ArgumentException("tempDir must not be empty", nameof(tempDir));
    }

    Directory.CreateDirectory(tempDir);
    var path = Path.Combine(tempDir, $"{Guid.NewGuid():N}{suffix}");
    File.WriteAllBytes(path, bytes);
    return new Blob(path);
  }

  /// <summary>
  /// Reads the blob bytes back from <paramref name="blobStorageFolder"/> using
  /// <see cref="Blob.GetLocalDestinationPath"/>. Falls back to <see cref="Blob.filePath"/>
  /// if the destination file does not yet exist (i.e. blob is still in its temp location).
  /// </summary>
  public static byte[] ReadAllBytes(this Blob blob, string blobStorageFolder)
  {
    _ = blob.NotNull();

    if (!string.IsNullOrEmpty(blobStorageFolder))
    {
      var dest = blob.GetLocalDestinationPath(blobStorageFolder);
      if (File.Exists(dest))
      {
        return File.ReadAllBytes(dest);
      }
    }

    if (!string.IsNullOrEmpty(blob.filePath) && File.Exists(blob.filePath))
    {
      return File.ReadAllBytes(blob.filePath);
    }

    throw new FileNotFoundException($"Blob {blob.id} not found in storage folder or temp path.");
  }
}
