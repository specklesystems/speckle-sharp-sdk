using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Security.Cryptography;
using System.Text;
#if NET6_0_OR_GREATER
using System.Runtime.InteropServices;
#endif

namespace Speckle.Sdk.Common;

public static class Sha256
{
#if NET6_0_OR_GREATER
  /// <param name="input">the value to hash</param>
  /// <param name="format"><c>"x2"</c> for lower case, <c>"X2"</c> for uppercase.</param>
  /// <param name="length">Desired length of the returned string. Must be 2 &#x2264; Length &#x2264; 64, and must be a multiple of 2</param>
  /// <returns><inheritdoc cref="GetString(string, string?, int)"/></returns>
  [Pure]
  public static string GetString(
    ReadOnlySpan<char> input,
    [StringSyntax(StringSyntaxAttribute.NumericFormat)] string? format = "x2",
    int length = SHA256.HashSizeInBytes * sizeof(char)
  )
  {
    ReadOnlySpan<byte> inputBytes = MemoryMarshal.AsBytes(input);

    Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
    SHA256.HashData(inputBytes, hash);

    Span<char> output = stackalloc char[length];

    for (int i = 0, j = 0; j < length; i += sizeof(byte), j += sizeof(char))
    {
      hash[i].TryFormat(output[j..], out _, format);
    }

    return new string(output);
  }
#endif

  /// <param name="input">the value to hash</param>
  /// <param name="format"><c>"x2"</c> for lower case, <c>"X2"</c> for uppercase.</param>
  /// <param name="length">Desired length of the returned string</param>
  /// <returns>the hash string</returns>
  /// <exception cref="FormatException"><paramref name="format"/> is not a recognised numeric format</exception>
  /// <exception cref="ArgumentOutOfRangeException"><inheritdoc cref="StringBuilder.ToString(int, int)"/></exception>
  [Pure]
  public static string GetString(
    string input,
    [StringSyntax(StringSyntaxAttribute.NumericFormat)] string? format = "x2",
    int length = 64
  )
  {
    var inputBytes = Encoding.Unicode.GetBytes(input);
#if NET6_0_OR_GREATER
    byte[] hash = SHA256.HashData(inputBytes);
#else
    using var sha256 = SHA256.Create();
    byte[] hash = sha256.ComputeHash(inputBytes);
#endif

    StringBuilder sb = new(64);
    foreach (byte b in hash)
    {
      sb.Append(b.ToString(format));
    }

    return sb.ToString(0, length);
  }
}
