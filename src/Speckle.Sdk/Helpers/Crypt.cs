using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Security.Cryptography;
using System.Text;

namespace Speckle.Sdk.Helpers;

public static class Crypt
{
  /// <param name="input">the value to hash</param>
  /// <param name="format">NumericFormat</param>
  /// <param name="startIndex"></param>
  /// <param name="length"></param>
  /// <returns>the hash string</returns>
#if NET6_0_OR_GREATER
  [Pure]
  public static string Sha256(
    ReadOnlySpan<char> input,
    [StringSyntax(StringSyntaxAttribute.NumericFormat)] string? format = "x2",
    int startIndex = 0,
    int length = 64
  )
  {
    Span<byte> inputBytes = stackalloc byte[Encoding.UTF8.GetByteCount(input)];
    Encoding.UTF8.GetBytes(input, inputBytes);

    Span<byte> hash = stackalloc byte[32]; // SHA256 produces 32-byte hash
    SHA256.HashData(inputBytes, hash);

    int outputLength = Math.Min(length, hash.Length - startIndex);
    Span<char> output = stackalloc char[outputLength * 2]; // Each byte is represented by two hex characters

    for (int i = 0; i < outputLength; i++)
    {
      hash[startIndex + i].TryFormat(output[(i * 2)..], out _, format);
    }

    return new string(output);
  }
#else
  /// <exception cref="FormatException"><paramref name="format"/> is not a recognised numeric format</exception>
  /// <exception cref="ArgumentOutOfRangeException"><inheritdoc cref="StringBuilder.ToString(int, int)"/></exception>
  [Pure]
  public static string Sha256(string input, string? format = "x2", int startIndex = 0, int length = 64)
  {
    var inputBytes = Encoding.UTF8.GetBytes(input);
    using var sha256 = SHA256.Create();
    byte[] hash = sha256.ComputeHash(inputBytes);

    StringBuilder sb = new(64);
    foreach (byte b in hash)
    {
      sb.Append(b.ToString(format));
    }

    return sb.ToString(startIndex, length);
  }
#endif

  /// <inheritdoc cref="Sha256"/>
  /// <remarks>MD5 is a broken cryptographic algorithm and should be used subject to review see CA5351</remarks>
  [Pure]
  [SuppressMessage("Security", "CA5351:Do Not Use Broken Cryptographic Algorithms")]
  public static string Md5(string input, string? format = "x2", int startIndex = 0, int length = 32)
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

    return sb.ToString(startIndex, length);
  }
}
