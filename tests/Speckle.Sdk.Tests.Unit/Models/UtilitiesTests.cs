using System.Collections;
using NUnit.Framework;
using Speckle.Sdk.Helpers;
using Speckle.Sdk.Models;

namespace Speckle.Sdk.Tests.Unit.Models;

[TestFixture(TestOf = typeof(Crypt))]
public sealed class HashUtilityTests
{
  public static IEnumerable<(string input, string sha256, string md5)> TestCases()
  {
    yield return (
      "fxFB14cBcXvoENN",
      "887db9349afa455f957a95f9dbacbb3c10697749cf4d4afc5c6398932a596fbc",
      "d38572fdb20fe90c4871178df3f9570d"
    );
    yield return (
      "tgWsOH8frdAwJT7",
      "e486224ded0dcb1452d69d0d005a6dcbc52087f6e8c66e04803e1337a192abb4",
      "a7eecf20d68f836f462963928cd0f1a1"
    );
    yield return (
      "wQKrSUzBB7FI1o6",
      "c8dc93bc5fcb299d8b39a66a52dd44705714859c2e8df044465088328c5e7d13",
      "2424cff4a88055b149e5ff2aaf0b3131"
    );
    yield return (
      "WnAbz1hCznVmDh1",
      "037275ac033edfb9d4c5b3248cc71299685f04674914b7b68a7a9ff35a78aab1",
      "ad48ff1e60ea2369de178aaab2fa99af"
    );
  }

  [Test, TestOf(nameof(Crypt.Md5))]
  public void Md5(
    [ValueSource(nameof(TestCases))] (string input, string _, string expected) testCase,
    [Range(32, 32)] int length
  )
  {
    var lower = Crypt.Md5(testCase.input, "x2", length);
    var upper = Crypt.Md5(testCase.input, "X2", length);

    Assert.That(lower, Is.EqualTo(new string(testCase.expected.ToLower()[..length])));
    Assert.That(upper, Is.EqualTo(new string(testCase.expected.ToUpper()[..length])));
  }

  [Test, TestOf(nameof(Crypt.Sha256))]
  public void Sha256(
    [ValueSource(nameof(TestCases))] (string input, string expected, string _) testCase,
    [Range(2, 64)] int length
  )
  {
    var lower = Crypt.Sha256(testCase.input, "x2", length);
    var upper = Crypt.Sha256(testCase.input, "X2", length);

    Assert.That(lower, Is.EqualTo(new string(testCase.expected.ToLower()[..length])));
    Assert.That(upper, Is.EqualTo(new string(testCase.expected.ToUpper()[..length])));
  }

  [Test, TestOf(nameof(Crypt.Sha256))]
  public void Sha256_Span(
    [ValueSource(nameof(TestCases))] (string input, string expected, string _) testCase,
    [Range(2, 64, 2)] int length //Span version of the function must have multiple of 2
  )
  {
    var lower64 = Crypt.Sha256(testCase.input.AsSpan(), "x2", length);
    var upper64 = Crypt.Sha256(testCase.input.AsSpan(), "X2", length);

    Assert.That(lower64, Is.EqualTo(new string(testCase.expected.ToLower()[..length])));
    Assert.That(upper64, Is.EqualTo(new string(testCase.expected.ToUpper()[..length])));
  }
}
