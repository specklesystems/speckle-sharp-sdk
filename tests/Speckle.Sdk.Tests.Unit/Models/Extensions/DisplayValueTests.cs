using FluentAssertions;
using Shouldly;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Models.Extensions;
using Xunit;

namespace Speckle.Sdk.Tests.Unit.Models.Extensions;

public class DisplayValueTests
{
  private const string PAYLOAD = "This is my payload";
  private static readonly Base s_displayValue = new() { applicationId = PAYLOAD };

  static DisplayValueTests()
  {
    Reset();
  }

  private static void Reset()
  {
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(Base).Assembly);
  }

  [Fact]
  public void Setup()
  {
    Reset();
  }

  [Theory]
  [MemberData(nameof(TestCases))]
  public void TestTryGetDisplayValue_WithValue(Base testCase)
  {
    var res = testCase.TryGetDisplayValue();

    // Assert collection count
    res?.Count.Should().Be(1);

    // Assert the single item matches the expected type and property
    var displayValue = res?[0];
    displayValue.ShouldNotBeNull();
    displayValue.applicationId.Should().Be(PAYLOAD);
  }

  public static IEnumerable<object[]> TestCases()
  {
    var listOfBase = new List<object> { s_displayValue }; // This is what our deserializer will output
    var listOfMesh = new List<Base> { s_displayValue };

    yield return new object[] { new Base { ["@displayValue"] = s_displayValue } };
    yield return new object[] { new Base { ["displayValue"] = s_displayValue } };
    yield return new object[] { new Base { ["@displayValue"] = listOfBase } };
    yield return new object[] { new Base { ["displayValue"] = listOfBase } };
    yield return new object[] { new TypedDisplayValue { displayValue = s_displayValue } };
    yield return new object[] { new TypedDisplayValueList { displayValue = listOfMesh } };
  }

  [SpeckleType("Speckle.Core.Tests.Unit.Models.Extensions.DisplayValueTests+TypedDisplayValue")]
  private class TypedDisplayValue : Base
  {
    public Base displayValue { get; set; }
  }

  [SpeckleType("Speckle.Core.Tests.Unit.Models.Extensions.DisplayValueTests+TypedDisplayValueList")]
  private class TypedDisplayValueList : Base
  {
    public List<Base> displayValue { get; set; }
  }
}
