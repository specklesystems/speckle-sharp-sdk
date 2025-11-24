using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
#if NET6_0_OR_GREATER
using System.Runtime.InteropServices;
#endif

namespace Speckle.Sdk.Common;

/// <summary>
/// Helpers for hashing data to a hex string
/// </summary>
public static class Sha256
{
  public const string DEFAULT_FORMAT = "x2";
  public const int HASH_SIZE_CHARS = 64; // SHA256.HashSizeInBytes * sizeof(char)
#if NET6_0_OR_GREATER
  /// <param name="input">the value to hash</param>
  /// <param name="destination">Output hash; it must have <c>2 &#x2264; Length &#x2264; 64</c>, and must be a multiple of 2</param>
  /// <param name="formatUpperCase"><see langword="true"/> for upper case, false otherwise</param>
  public static void Hash(ReadOnlySpan<char> input, bool formatUpperCase, Span<char> destination)
  {
    ReadOnlySpan<byte> inputBytes = MemoryMarshal.AsBytes(input);
    Hash(inputBytes, formatUpperCase, destination);
  }

  public static void Hash(ReadOnlySpan<byte> input, bool formatUpperCase, Span<char> destination)
  {
    Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
    SHA256.HashData(input, hash);

    FormatHash(hash, formatUpperCase, destination);
  }

  public static void Hash(Stream source, bool formatUpperCase, Span<char> destination)
  {
    Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
    SHA256.HashData(source, hash);

    FormatHash(hash, formatUpperCase, destination);
  }

  private static void FormatHash(ReadOnlySpan<byte> input, bool formatUpperCase, Span<char> output)
  {
    for (int i = 0, j = 0; j < output.Length; i += sizeof(byte), j += sizeof(char))
    {
      input[i].TryFormat(output[j..], out _, formatUpperCase ? "X2" : "x2");
    }
  }
#endif

  /// <param name="input">the value to hash</param>
  /// <param name="format"><c>"x2"</c> for lower case, <c>"X2"</c> for uppercase.</param>
  /// <param name="outputLengthChars">Desired length of the returned string</param>
  /// <returns>the hash string</returns>
  /// <exception cref="ArgumentOutOfRangeException"><inheritdoc cref="StringBuilder.ToString(int, int)"/></exception>
  [Pure]
  public static string Hash(
    string input,
    [StringSyntax(StringSyntaxAttribute.NumericFormat)] string? format = DEFAULT_FORMAT,
    int outputLengthChars = HASH_SIZE_CHARS
  )
  {
    var inputBytes = Encoding.Unicode.GetBytes(input);
#if NET6_0_OR_GREATER
    byte[] hash = SHA256.HashData(inputBytes);
#else
    using var sha256 = SHA256.Create();
    byte[] hash = sha256.ComputeHash(inputBytes);
#endif

    StringBuilder sb = new(HASH_SIZE_CHARS);
    foreach (byte b in hash)
    {
      sb.Append(b.ToString(format));
    }

    return sb.ToString(0, outputLengthChars);
  }

  /// <inheritdoc cref="Hash(string, string?, int)"/>
  [Pure]
  public static string Hash(
    Stream input,
    [StringSyntax(StringSyntaxAttribute.NumericFormat)] string? format = DEFAULT_FORMAT,
    int outputLengthChars = HASH_SIZE_CHARS
  )
  {
#if NET6_0_OR_GREATER
    byte[] hash = SHA256.HashData(input);
#else
    using var sha256 = SHA256.Create();
    byte[] hash = sha256.ComputeHash(input);
#endif

    return FormatHash(hash, format, outputLengthChars);
  }

  [Pure]
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  private static string FormatHash(byte[] hash, string? format, int outputLengthChars)
  {
    StringBuilder sb = new(HASH_SIZE_CHARS);
    foreach (byte b in hash)
    {
      sb.Append(b.ToString(format));
    }

    return sb.ToString(0, outputLengthChars);
  }
}
