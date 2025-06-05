using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;

namespace Speckle.Sdk.Tests.Unit.Common;

public class Md5Tests
{
  [Theory]
  [InlineData("", "d41d8cd98f00b204e9800998ecf8427e")]
  [InlineData("a", "0cc175b9c0f1b6a831c399e269772661")]
  [InlineData("abc", "900150983cd24fb0d6963f7d28e17f72")]
  [InlineData("message digest", "f96b697d7cb7938d525a2f31aaf161d0")]
  [InlineData("abcdefghijklmnopqrstuvwxyz", "c3fcd3d76192e4007dfb496cca67e13b")]
  [InlineData("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789", "d174ab98d277d9f5a5611c2c9f419d9f")]
  [InlineData(
    "12345678901234567890123456789012345678901234567890123456789012345678901234567890",
    "57edf4a22be3c955ac49da2e2107b67a"
  )]
  public void Md5_Hash_Is_Correct(string input, string expectedHex)
  {
    string actual = Speckle.Sdk.Common.Md5.GetString(input).ToLowerInvariant();
    expectedHex.Should().Be(actual);
  }

  [Theory]
  [InlineData("", "d41d8cd98f00b204e9800998ecf8427e")]
  [InlineData("a", "0cc175b9c0f1b6a831c399e269772661")]
  [InlineData("abc", "900150983cd24fb0d6963f7d28e17f72")]
  [InlineData("message digest", "f96b697d7cb7938d525a2f31aaf161d0")]
  [InlineData("abcdefghijklmnopqrstuvwxyz", "c3fcd3d76192e4007dfb496cca67e13b")]
  [InlineData("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789", "d174ab98d277d9f5a5611c2c9f419d9f")]
  [InlineData(
    "12345678901234567890123456789012345678901234567890123456789012345678901234567890",
    "57edf4a22be3c955ac49da2e2107b67a"
  )]
  public void Md5_Compare(string input, string expectedHex)
  {
    //old always did to lower for some reason.
    string actual = Speckle.Sdk.Common.Md5.GetString(input).ToLowerInvariant();
    string old = OldMd5(input);
    expectedHex.Should().Be(actual);
    expectedHex.Should().Be(old);
  }

  [Pure]
  [SuppressMessage("Security", "CA5351:Do Not Use Broken Cryptographic Algorithms")]
  public static string OldMd5(
    string input,
    [StringSyntax(StringSyntaxAttribute.NumericFormat)] string? format = "x2",
    int length = 32
  )
  {
    byte[] inputBytes = Encoding.ASCII.GetBytes(input);
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
