using NUnit.Framework;
using Speckle.Sdk.Common;

namespace Speckle.Sdk.Tests.Unit.Common;

public class UnitsTest
{
  private const double EPS = 0.00022;

  public static List<string> OfficiallySupportedUnits => Units.SupportedUnits;
  public static List<string> NotSupportedUnits => ["feeters", "liters", "us_ft"];
  public static List<string?> ConversionSupport => [.. Units.SupportedUnits, null];

  [Test, Combinatorial]
  [DefaultFloatingPointTolerance(EPS)]
  public void TestUnitConversion(
    [ValueSource(nameof(ConversionSupport))] string? from,
    [ValueSource(nameof(ConversionSupport))] string? to
  )
  {
    var forwards = Units.GetConversionFactor(from, to);
    var backwards = Units.GetConversionFactor(to, from);

    Assert.That(
      backwards * forwards,
      Is.EqualTo(1d),
      $"Behaviour says that 1{from} == {forwards}{to}, and 1{to} == {backwards}{from}"
    );
  }

  [TestCaseSource(nameof(OfficiallySupportedUnits))]
  public void IsUnitSupported_ReturnsTrue_AllSupportedUnits(string unit)
  {
    bool res = Units.IsUnitSupported(unit);
    Assert.That(res, Is.True);
  }

  [TestCaseSource(nameof(NotSupportedUnits))]
  public void IsUnitSupported_ReturnsFalse_NotSupportedUnits(string unit)
  {
    bool res = Units.IsUnitSupported(unit);
    Assert.That(res, Is.False);
  }

  [TestCaseSource(nameof(OfficiallySupportedUnits))]
  public void GetUnitsFromString_ReturnsSupported(string unit)
  {
    var lower = Units.GetUnitsFromString(unit);
    var upper = Units.GetUnitsFromString(unit?.ToUpperInvariant());

    Assert.That(lower, Is.EqualTo(unit));
    Assert.That(upper, Is.EqualTo(unit));
  }

  [TestCaseSource(nameof(NotSupportedUnits))]
  public void GetUnitsFromString_ThrowsUnSupported(string unit)
  {
    Assert.Throws<ArgumentOutOfRangeException>(() => _ = Units.GetUnitsFromString(unit));
  }

  [TestCaseSource(nameof(OfficiallySupportedUnits))]
  public void UnitEncoding_RoundTrip(string unit)
  {
    var encoded = Units.GetEncodingFromUnit(unit);
    var res = Units.GetUnitFromEncoding(encoded);

    Assert.That(res, Is.EqualTo(unit));
  }
}
