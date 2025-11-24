using System.Diagnostics.Contracts;
using Speckle.Sdk.Common;
using Speckle.Sdk.Serialisation;

namespace Speckle.Sdk.Models;

/// <summary>
/// Helper functions for calculating hash based Ids for Speckle core concepts
/// </summary>
public static class HashUtility
{
  public const int HASH_LENGTH_CHARS = 32;

  [Pure]
  public static Id ComputeObjectId(Json serialized)
  {
#if NET6_0_OR_GREATER
    Span<char> hash = stackalloc char[HASH_LENGTH_CHARS];
    Sha256.Hash(serialized.Value.AsSpan(), false, hash);
    return new Id(new string(hash));
#else
    string hash = Sha256.Hash(serialized.Value, outputLengthChars: HashUtility.HASH_LENGTH_CHARS);
    return new Id(hash);
#endif
  }

  [Pure]
  public static string CalculateBlobHash(string filePath)
  {
    using var stream = File.OpenRead(filePath);
#if NET6_0_OR_GREATER
    Span<char> hash = stackalloc char[HASH_LENGTH_CHARS];
    Sha256.Hash(stream, false, hash);
    return new(hash);
#else
    return Sha256.Hash(stream, "x2", HASH_LENGTH_CHARS);
#endif
  }
}
