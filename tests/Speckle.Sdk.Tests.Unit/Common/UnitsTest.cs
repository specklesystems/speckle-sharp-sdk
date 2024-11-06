using System.Collections;
using NUnit.Framework;
using Shouldly;
using Speckle.Sdk.Common;
using Xunit;

namespace Speckle.Sdk.Tests.Unit.Common;

public class UnitsTest
{
  public static List<object[]> OfficiallySupportedUnits => Units.SupportedUnits.Select(x => new object[] { x }).ToList();
  public static List<object[]> NotSupportedUnits => new [] {"feeters", "liters", "us_ft"}.Select(x => new object[] { x }).ToList();
  public static List<object[]?> ConversionSupport => [.. OfficiallySupportedUnits, null];

  public static List<string?> ConversionSupportStrings => [.. Units.SupportedUnits, null];
  public static IEnumerable<object?[]> TestUnitConversionData
  {
    get
    {
      foreach (var from in ConversionSupportStrings)
      {
        foreach (var to in ConversionSupportStrings)
        {
          yield return [from, to];
        }
      }
    }
  }
  [Theory]
  [MemberData(nameof(TestUnitConversionData))]
  public void TestUnitConversion( string? from,string? to)
  {
    var forwards = Units.GetConversionFactor(from, to);
    var backwards = Units.GetConversionFactor(to, from);

   (
      backwards * forwards).ShouldBe(1d,
      $"Behaviour says that 1{from} == {forwards}{to}, and 1{to} == {backwards}{from}"
    );
  }

  [Theory]
  [MemberData(nameof(OfficiallySupportedUnits))]
  public void IsUnitSupported_ReturnsTrue_AllSupportedUnits(string unit)
  {
    bool res = Units.IsUnitSupported(unit);
    res.ShouldBeTrue();
  }

  [Theory]
  [MemberData(nameof(NotSupportedUnits))]
  public void IsUnitSupported_ReturnsFalse_NotSupportedUnits(string unit)
  {
    bool res = Units.IsUnitSupported(unit);
    res.ShouldBeFalse();
  }

  [Theory]
  [MemberData(nameof(OfficiallySupportedUnits))]
  public void GetUnitsFromString_ReturnsSupported(string unit)
  {
    var lower = Units.GetUnitsFromString(unit);
    var upper = Units.GetUnitsFromString(unit?.ToUpperInvariant());

    lower.ShouldBe(unit);
    upper.ShouldBe(unit);
  }


  [Theory]
  [MemberData(nameof(NotSupportedUnits))]
  public void GetUnitsFromString_ThrowsUnSupported(string unit)
  {
    Assert.Throws<ArgumentOutOfRangeException>(() => _ = Units.GetUnitsFromString(unit));
  }


  [Theory]
  [MemberData(nameof(OfficiallySupportedUnits))]
  public void UnitEncoding_RoundTrip(string unit)
  {
    var encoded = Units.GetEncodingFromUnit(unit);
    var res = Units.GetUnitFromEncoding(encoded);

    res.ShouldBe(unit);
  }
}
