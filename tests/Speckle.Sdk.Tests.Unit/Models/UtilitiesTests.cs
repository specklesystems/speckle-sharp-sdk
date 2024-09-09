using NUnit.Framework;
using Speckle.Sdk.Helpers;

namespace Speckle.Sdk.Tests.Unit.Models;

[TestFixture(TestOf = typeof(Crypt))]
public sealed class HashUtilityTests
{
  public static IEnumerable<(string input, string sha256, string md5)> SmallTestCases()
  {
    yield return (
      "fxFB14cBcXvoENN",
      "491267c87e343c2a4f9070034f4f8966e8ee4c14e5baf6f49289833142e5b509",
      "d38572fdb20fe90c4871178df3f9570d"
    );
    yield return (
      "tgWsOH8frdAwJT7",
      "dd62d2028d8243f07cbdbb0cd4c3460a96c88dd6322dd9fceba4e4912ad88fa7",
      "a7eecf20d68f836f462963928cd0f1a1"
    );
    yield return (
      "wQKrSUzBB7FI1o6",
      "70be5055f737e05d287c8898c7fcd3342733a337b67fe64f91fd34dcdf92fc88",
      "2424cff4a88055b149e5ff2aaf0b3131"
    );
    yield return (
      "WnAbz1hCznVmDh1",
      "511433f4bb8d24d4ef7d4478984fd36f17ab6c58676f40ad0f4bcb615de0e313",
      "ad48ff1e60ea2369de178aaab2fa99af"
    );
  }

  public static IEnumerable<(string input, string sha256, string md5)> LargeTestCases()
  {
    Random random = new(420);
    yield return (
      new string(Enumerable.Range(0, 1_000_000).Select(_ => (char)random.Next(32, 127)).ToArray()),
      "b919b9e60cd6bb86ab395ee1408e12efd4d3e4e7b58f02b4cda6b4120086959a",
      "d38572fdb20fe90c4871178df3f9570d"
    );
    yield return (
      new string(Enumerable.Range(0, 10_000_000).Select(_ => (char)random.Next(32, 127)).ToArray()),
      "f2e83101c3066c8a2983acdb92df53504ec00ac1e5afb71b7c3798cb4daf6162",
      "a7eecf20d68f836f462963928cd0f1a1"
    );
  }

  [Test, TestOf(nameof(Crypt.Md5))]
  public void Md5(
    [ValueSource(nameof(SmallTestCases))] (string input, string _, string expected) testCase,
    [Range(0, 32)] int length
  )
  {
    var lower = Crypt.Md5(testCase.input, "x2", length);
    var upper = Crypt.Md5(testCase.input, "X2", length);

    Assert.That(lower, Is.EqualTo(new string(testCase.expected.ToLower()[..length])));
    Assert.That(upper, Is.EqualTo(new string(testCase.expected.ToUpper()[..length])));
  }

  [Test, TestOf(nameof(Crypt.Sha256))]
  public void Sha256(
    [ValueSource(nameof(SmallTestCases))] (string input, string expected, string _) testCase,
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
    [ValueSource(nameof(SmallTestCases))] (string input, string expected, string _) testCase,
    [Range(2, 64, 2)] int length //Span version of the function must have multiple of 2
  )
  {
    var lower64 = Crypt.Sha256(testCase.input.AsSpan(), "x2", length);
    var upper64 = Crypt.Sha256(testCase.input.AsSpan(), "X2", length);

    Assert.That(lower64, Is.EqualTo(new string(testCase.expected.ToLower()[..length])));
    Assert.That(upper64, Is.EqualTo(new string(testCase.expected.ToUpper()[..length])));
  }

  [Test, TestOf(nameof(Crypt.Sha256))]
  [TestCaseSource(nameof(LargeTestCases))]
  public void Sha256_LargeDataTests((string input, string expected, string _) testCase)
  {
    var test = Crypt.Sha256(testCase.input.AsSpan());

    Assert.That(test, Is.EqualTo(testCase.expected));
  }
}
