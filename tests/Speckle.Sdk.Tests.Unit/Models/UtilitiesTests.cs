using FluentAssertions;
using NUnit.Framework;
using Shouldly;
using Speckle.Sdk.Dependencies;
using Speckle.Sdk.Helpers;
using Xunit;

namespace Speckle.Sdk.Tests.Unit.Models;

public sealed class HashUtilityTests
{
  public static IEnumerable<object[]> SmallTestCases()
  {
    yield return
    [
      "fxFB14cBcXvoENN",
      "491267c87e343c2a4f9070034f4f8966e8ee4c14e5baf6f49289833142e5b509",
      "d38572fdb20fe90c4871178df3f9570d",
    ];
    yield return
    [
      "tgWsOH8frdAwJT7",
      "dd62d2028d8243f07cbdbb0cd4c3460a96c88dd6322dd9fceba4e4912ad88fa7",
      "a7eecf20d68f836f462963928cd0f1a1",
    ];
    yield return
    [
      "wQKrSUzBB7FI1o6",
      "70be5055f737e05d287c8898c7fcd3342733a337b67fe64f91fd34dcdf92fc88",
      "2424cff4a88055b149e5ff2aaf0b3131",
    ];
    yield return
    [
      "WnAbz1hCznVmDh1",
      "511433f4bb8d24d4ef7d4478984fd36f17ab6c58676f40ad0f4bcb615de0e313",
      "ad48ff1e60ea2369de178aaab2fa99af",
    ];
  }

  public static IEnumerable<object[]> SmallTestCases(IEnumerable<object[]> cases, IEnumerable<int> range)
  {
    foreach (var length in range)
    {
      foreach (var testCase in cases)
      {
        yield return [.. testCase, length];
      }
    }
  }

  public static IEnumerable<object[]> SmallTestCasesMd5()
  {
    return SmallTestCases(SmallTestCases(), EnumerableExtensions.RangeFrom(0, 32));
  }

  public static IEnumerable<object[]> SmallTestCasesSha256()
  {
    return SmallTestCases(SmallTestCases(), EnumerableExtensions.RangeFrom(2, 64));
  }

  public static IEnumerable<object[]> SmallTestCasesSha256Span()
  {
    return SmallTestCases(SmallTestCases(), EnumerableExtensions.RangeFrom(2, 64).Where(x => x % 2 == 0));
  }

  public static IEnumerable<object[]> LargeTestCases()
  {
    Random random = new(420);
    yield return
    [
      new string(Enumerable.Range(0, 1_000_000).Select(_ => (char)random.Next(32, 127)).ToArray()),
      "b919b9e60cd6bb86ab395ee1408e12efd4d3e4e7b58f02b4cda6b4120086959a",
    ];
    yield return
    [
      new string(Enumerable.Range(0, 10_000_000).Select(_ => (char)random.Next(32, 127)).ToArray()),
      "f2e83101c3066c8a2983acdb92df53504ec00ac1e5afb71b7c3798cb4daf6162",
    ];
  }

  [Theory]
  [MemberData(nameof(SmallTestCasesMd5))]
  public void Md5(string input, string _, string expected, int length)
  {
    var resultLower = Crypt.Md5(input, "x2", length);
    var resultUpper = Crypt.Md5(input, "X2", length);

    resultLower.Should().Be(new string(expected.ToLower()[..length]));

    resultUpper.Should().Be(new string(expected.ToUpper()[..length]));
  }

  [Theory]
  [MemberData(nameof(SmallTestCasesSha256))]
  public void Sha256(string input, string expected, string _, int length)
  {
    var resultLower = Crypt.Sha256(input, "x2", length);
    var resultUpper = Crypt.Sha256(input, "X2", length);

    resultLower.Should().Be(new string(expected.ToLower()[..length]));

    resultUpper.Should().Be(new string(expected.ToUpper()[..length]));
  }

  [Theory]
  [MemberData(nameof(SmallTestCasesSha256Span))]
  public void Sha256_Span(
    string input,
    string expected,
    string _,
    int length //Span version of the function must have multiple of 2
  )
  {
    var resultLowerSpan = Crypt.Sha256(input.AsSpan(), "x2", length);
    var resultUpperSpan = Crypt.Sha256(input.AsSpan(), "X2", length);

    resultLowerSpan.Should().Be(new string(expected.ToLower()[..length]));

    resultUpperSpan.Should().Be(new string(expected.ToUpper()[..length]));
  }

  [Theory]
  [MemberData(nameof(LargeTestCases))]
  public void Sha256_LargeDataTests(string input, string expected)
  {
    var computedHash = Crypt.Sha256(input.AsSpan());
    computedHash.Should().Be(expected);
  }
}
