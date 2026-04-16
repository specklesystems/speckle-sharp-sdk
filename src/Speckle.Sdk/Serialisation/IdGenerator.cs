using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Security.Cryptography;
using System.Text;
using Speckle.Sdk.Common;
using Speckle.Sdk.Models;

namespace Speckle.Sdk.Serialisation;

public static class IdGenerator
{
  [Pure]
  //Obsolete (serializer v2)
  public static Id ComputeId(Json serialized)
  {
#if NET6_0_OR_GREATER
    string hash = Sha256.GetString(serialized.Value.AsSpan(), length: HashUtility.HASH_LENGTH);
#else
    string hash = Sha256.GetString(serialized.Value, length: HashUtility.HASH_LENGTH);
#endif
    return new Id(hash);
  }

#if NET6_0_OR_GREATER
  [Pure]
  public static string ComputeId(ReadOnlySpan<byte> input)
  {
    Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
    SHA256.HashData(input, hash);

    Span<char> output = stackalloc char[HashUtility.HASH_LENGTH];

    for (int i = 0, j = 0; j < HashUtility.HASH_LENGTH; i += sizeof(byte), j += sizeof(char))
    {
      hash[i].TryFormat(output[j..], out _, "x2");
    }

    return new string(output);
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
