using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Security.Cryptography;
using System.Text;

namespace Speckle.Sdk.Pipelines.Send;

public static class IdGenerator
{
  public const int ID_HEX_LENGTH_CHARS = 32;

#if NET6_0_OR_GREATER
  [Pure]
  public static string ComputeId(ReadOnlySpan<byte> input)
  {
    Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
    SHA256.HashData(input, hash);

#if NET9_0_OR_GREATER
    return Convert.ToHexStringLower(hash[..(SHA256.HashSizeInBytes / 2)]);
#else
    Span<char> output = stackalloc char[ID_HEX_LENGTH_CHARS];

    for (int i = 0, j = 0; j < ID_HEX_LENGTH_CHARS; i += sizeof(byte), j += sizeof(char))
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

    return sb.ToString(0, ID_HEX_LENGTH_CHARS);
  }
}
