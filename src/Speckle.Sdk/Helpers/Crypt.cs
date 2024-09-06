using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Security.Cryptography;
using System.Text;

namespace Speckle.Sdk.Helpers;

public static class Crypt
{
#if NET6_0_OR_GREATER
  /// <param name="input">the value to hash</param>
  /// <param name="format"><c>"x2"</c> for lower case, <c>"X2"</c> for uppercase.</param>
  /// <param name="length">Desired length of the returned string. Must be 2 &#x2264; Length &#x2264; 64, and must be a multiple of 2</param>
  /// <returns>the hash string</returns>
  [Pure]
  public static string Sha256(
    ReadOnlySpan<char> input,
    [StringSyntax(StringSyntaxAttribute.NumericFormat)] string? format = "x2",
    int length = SHA256.HashSizeInBytes * sizeof(char)
  )
  {
    Span<byte> inputBytes = stackalloc byte[Encoding.UTF8.GetByteCount(input)];
    Encoding.UTF8.GetBytes(input, inputBytes);

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
  /// <param name="format">NumericFormat</param>
  /// <param name="length"></param>
  /// <returns>the hash string</returns>
  /// <exception cref="FormatException"><paramref name="format"/> is not a recognised numeric format</exception>
  /// <exception cref="ArgumentOutOfRangeException"><inheritdoc cref="StringBuilder.ToString(int, int)"/></exception>
  [Pure]
  public static string Sha256(string input, string? format = "x2", int length = 64)
  {
    var inputBytes = Encoding.UTF8.GetBytes(input);
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

  /// <inheritdoc cref="Sha256(string, string?, int)"/>
  /// <remarks>MD5 is a broken cryptographic algorithm and should be used subject to review see CA5351</remarks>
  [Pure]
  [SuppressMessage("Security", "CA5351:Do Not Use Broken Cryptographic Algorithms")]
  public static string Md5(string input, string? format = "x2", int length = 32)
  {
    byte[] inputBytes = Encoding.ASCII.GetBytes(input.ToLowerInvariant());
#if NETSTANDARD2_0
    using MD5 md5 = MD5.Create();
    byte[] hashBytes = md5.ComputeHash(inputBytes);
#else
    byte[] hashBytes = MD5.HashData(inputBytes);
#endif
    StringBuilder sb = new(32);
    for (int i = 0; i < hashBytes.Length; i++)
    {
      sb.Append(hashBytes[i].ToString(format));
    }

    return sb.ToString(0, length);
  }
}
