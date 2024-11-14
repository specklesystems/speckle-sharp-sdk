using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Models.Extensions;

namespace Speckle.Sdk.Tests.Unit.Models.Extensions;

[TestOf(typeof(BaseExtensions))]
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

  [SetUp]
  public void Setup() => Reset();

  [TestCaseSource(nameof(TestCases))]
  public void TestTryGetDisplayValue_WithValue(Base testCase)
  {
    var res = testCase.TryGetDisplayValue();

    Assert.That(res, Has.Count.EqualTo(1));
    Assert.That(res, Has.One.Items.TypeOf<Base>().With.Property(nameof(Base.applicationId)).EqualTo(PAYLOAD));
  }

  public static IEnumerable<Base> TestCases()
  {
    var listOfBase = new List<object> { s_displayValue }; //This is what our deserializer will output
    var listOfMesh = new List<Base> { s_displayValue };
    yield return new Base { ["@displayValue"] = s_displayValue };
    yield return new Base { ["displayValue"] = s_displayValue };
    yield return new Base { ["@displayValue"] = listOfBase };
    yield return new Base { ["displayValue"] = listOfBase };
    yield return new TypedDisplayValue { displayValue = s_displayValue };
    yield return new TypedDisplayValueList { displayValue = listOfMesh };
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
