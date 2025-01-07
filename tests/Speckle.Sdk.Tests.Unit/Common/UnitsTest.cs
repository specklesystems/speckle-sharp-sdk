using FluentAssertions;

using Speckle.Sdk.Common;
using Xunit;

namespace Speckle.Sdk.Tests.Unit.Common;

public class UnitsTest
{
  private const double EPS = 0.00022;

  public static List<string> OfficiallySupportedUnits => Units.SupportedUnits;

  public static List<string> NotSupportedUnits => ["feeters", "liters", "us_ft"];

  public static List<string?> ConversionSupport => Units.SupportedUnits.Concat([null]).ToList();

  [Theory]
  [MemberData(nameof(ConversionSupportGenerator))]
  public void TestUnitConversion(string? from, string? to)
  {
    var forwards = Units.GetConversionFactor(from, to);
    var backwards = Units.GetConversionFactor(to, from);

    (backwards * forwards).Should().BeApproximately(
      1d,
       EPS,
      $"Behaviour says that 1{from} == {forwards}{to}, and 1{to} == {backwards}{from}"
    );
  }

  [Theory]
  [MemberData(nameof(OfficiallySupportedUnitsGenerator))]
  public void IsUnitSupported_ReturnsTrue_AllSupportedUnits(string unit)
  {
    bool res = Units.IsUnitSupported(unit);
    res.Should().BeTrue();
  }

  [Theory]
  [MemberData(nameof(NotSupportedUnitsGenerator))]
  public void IsUnitSupported_ReturnsFalse_NotSupportedUnits(string unit)
  {
    bool res = Units.IsUnitSupported(unit);
    res.Should().BeFalse();
  }

  [Theory]
  [MemberData(nameof(OfficiallySupportedUnitsGenerator))]
  public void GetUnitsFromString_ReturnsSupported(string unit)
  {
    string? lower = Units.GetUnitsFromString(unit);
    string? upper = Units.GetUnitsFromString(unit.ToUpperInvariant());

    lower.Should().Be(unit);
    upper.Should().Be(unit);
  }

  [Theory]
  [MemberData(nameof(NotSupportedUnitsGenerator))]
  public void GetUnitsFromString_ThrowsUnSupported(string unit)
  {
    FluentActions.Invoking(() => Units.GetUnitsFromString(unit)).Should().Throw<ArgumentOutOfRangeException>();
  }

  [Theory]
  [MemberData(nameof(OfficiallySupportedUnitsGenerator))]
  public void UnitEncoding_RoundTrip(string unit)
  {
    var encoded = Units.GetEncodingFromUnit(unit);
    var res = Units.GetUnitFromEncoding(encoded);

    res.Should().Be(unit);
  }

  // Generators for MemberData
  public static IEnumerable<object[]> OfficiallySupportedUnitsGenerator()
  {
    foreach (var unit in OfficiallySupportedUnits)
    {
      yield return [unit];
    }
  }

  public static IEnumerable<object[]> NotSupportedUnitsGenerator()
  {
    foreach (var unit in NotSupportedUnits)
    {
      yield return [unit];
    }
  }

  public static IEnumerable<object?[]> ConversionSupportGenerator()
  {
    foreach (var from in ConversionSupport)
    {
      foreach (var to in ConversionSupport)
      {
        yield return [from, to];
      }
    }
  }
}
