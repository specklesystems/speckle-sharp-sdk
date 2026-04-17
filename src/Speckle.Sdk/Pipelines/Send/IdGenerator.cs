using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Security.Cryptography;
using System.Text;
using Speckle.Sdk.Models;

namespace Speckle.Sdk.Pipelines.Send;

public static class IdGenerator
{
#if NET6_0_OR_GREATER
  [Pure]
  public static string ComputeId(ReadOnlySpan<byte> input)
  {
    Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
    SHA256.HashData(input, hash);

#if NET9_0_OR_GREATER
    return Convert.ToHexStringLower(hash);
#else
    Span<char> output = stackalloc char[HashUtility.HASH_LENGTH];

    for (int i = 0, j = 0; j < HashUtility.HASH_LENGTH; i += sizeof(byte), j += sizeof(char))
    {
      hash[i].TryFormat(output[j..], out _, "x2");
    }
    return new string(output);
#endif
  }
#endif

  [Pure]
  [SuppressMessage(
    "Performance",
    "CA1850:Prefer static \'HashData\' method over \'ComputeHash\'",
    Justification = "We expose another overload with higher performance spans"
  )]
  public static string ComputeId(byte[] input, int offset, int count)
  {
    using var sha256 = SHA256.Create();
    byte[] hash = sha256.ComputeHash(input, offset, count);

    StringBuilder sb = new(64);
    foreach (byte b in hash)
    {
      sb.Append(b.ToString("x2"));
    }

    return sb.ToString(0, HashUtility.HASH_LENGTH);
  }
}
