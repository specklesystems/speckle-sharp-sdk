using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

namespace Speckle.Sdk.Models;

public static class HashUtility
{
  public enum HashingFunctions
  {
    SHA256,
    MD5,
  }

  public const int HASH_LENGTH = 32;

  [SuppressMessage("Security", "CA5351:Do Not Use Broken Cryptographic Algorithms")]
  public static string HashFile(string filePath, HashingFunctions func = HashingFunctions.SHA256)
  {
    using HashAlgorithm hashAlgorithm = func == HashingFunctions.MD5 ? MD5.Create() : SHA256.Create();

    using var stream = File.OpenRead(filePath);

    var hash = hashAlgorithm.ComputeHash(stream);
    return BitConverter.ToString(hash, 0, HASH_LENGTH).Replace("-", "").ToLowerInvariant();
  }
}
