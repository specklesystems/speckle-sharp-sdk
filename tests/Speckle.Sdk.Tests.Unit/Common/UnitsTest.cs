using Shouldly;
using Speckle.Sdk.Common;

namespace Speckle.Sdk.Tests.Unit.Common;

public class UnitsTest
{
  public static List<string> OfficiallySupportedUnits() => Units.SupportedUnits;

  public static List<string> NotSupportedUnits() => new[] { "feeters", "liters", "us_ft" }.ToList();

  public static List<string?> ConversionSupport => [.. OfficiallySupportedUnits(), null];

  public static List<string?> ConversionSupportStrings => [.. Units.SupportedUnits, null];

  public static IEnumerable<(string?, string?)> TestUnitConversionData()
  {
    foreach (var from in ConversionSupportStrings)
    {
      foreach (var to in ConversionSupportStrings)
      {
        yield return (from, to);
      }
    }
  }

  [Test]
  [MethodDataSource(nameof(TestUnitConversionData))]
  public void TestUnitConversion(string? from, string? to)
  {
    var forwards = Units.GetConversionFactor(from, to);
    var backwards = Units.GetConversionFactor(to, from);

    (backwards * forwards).ShouldBe(
      1d,
      0.001d,
      $"Behaviour says that 1{from} == {forwards}{to}, and 1{to} == {backwards}{from}"
    );
  }

  [Test]
  [MethodDataSource(nameof(OfficiallySupportedUnits))]
  public void IsUnitSupported_ReturnsTrue_AllSupportedUnits(string unit)
  {
    bool res = Units.IsUnitSupported(unit);
    res.ShouldBeTrue();
  }

  [Test]
  [MethodDataSource(nameof(NotSupportedUnits))]
  public void IsUnitSupported_ReturnsFalse_NotSupportedUnits(string unit)
  {
    bool res = Units.IsUnitSupported(unit);
    res.ShouldBeFalse();
  }

  [Test]
  [MethodDataSource(nameof(OfficiallySupportedUnits))]
  public void GetUnitsFromString_ReturnsSupported(string unit)
  {
    var lower = Units.GetUnitsFromString(unit);
    var upper = Units.GetUnitsFromString(unit?.ToUpperInvariant());

    lower.ShouldBe(unit);
    upper.ShouldBe(unit);
  }

  [Test]
  [MethodDataSource(nameof(NotSupportedUnits))]
  public void GetUnitsFromString_ThrowsUnSupported(string unit)
  {
    Assert.Throws<ArgumentOutOfRangeException>(() => _ = Units.GetUnitsFromString(unit));
  }

  [Test]
  [MethodDataSource(nameof(OfficiallySupportedUnits))]
  public void UnitEncoding_RoundTrip(string unit)
  {
    var encoded = Units.GetEncodingFromUnit(unit);
    var res = Units.GetUnitFromEncoding(encoded);

    res.ShouldBe(unit);
  }
}
