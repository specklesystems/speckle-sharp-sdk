using FluentAssertions;
using Speckle.DoubleNumerics;
using Speckle.Objects.Geometry;

namespace Speckle.Objects.Tests.Unit.Geometry;

public class VectorTests
{
  private const float FLOAT_TOLERANCE = 1e-6f;

  public static IReadOnlyList<object[]> TestCases() =>
    [
      [0d, 0d, 0d, "m"],
      [1d, 2d, 3d, "ft"],
      [0d, 0d, -1d, "km"],
      [100d, 0d, -200d, "in"],
      [123.123d, 456.456d, 5789.789d, "cm"],
      [-123.123d, -456.456d, -5789.789d, "mm"],
    ];

  [Theory]
  [MemberData(nameof(TestCases))]
  public void Constructors_AreTheSame(double x, double y, double z, string units)
  {
    const string appId = "asdfasdfasdf";
    var pctor = new Vector(x, y, z, units, applicationId: appId);

    pctor.x.Should().Be(x);
    pctor.y.Should().Be(y);
    pctor.z.Should().Be(z);
    pctor.units.Should().Be(units);
    pctor.applicationId.Should().Be(appId);

    var init = new Vector
    {
      x = x,
      y = y,
      z = z,
      units = units,
      applicationId = appId,
    };

    Assert.Equal(pctor.x, init.x);
    Assert.Equal(pctor.y, init.y);
    Assert.Equal(pctor.z, init.z);
    Assert.Equal(pctor.units, init.units);
    Assert.Equal(pctor.applicationId, init.applicationId);
  }

  [Theory]
  [InlineData(1d, 0d, 0d, 1d)]
  [InlineData(0d, 2d, 0d, 2d)]
  [InlineData(0d, 0d, -3d, 3d)]
  [InlineData(1d, 1d, 0d, 1.4142135623730951d)]
  public void LengthCalculated(double x, double y, double z, double expected)
  {
    var testCase = new Vector(x, y, z, "");
    var actual = testCase.Length;
    actual.Should().Be(expected);
  }

  [Theory]
  [MemberData(nameof(TestCases))]
  public void EncodingToAndFromList(double x, double y, double z, string units)
  {
    var testCase = new Vector(x, y, z, units);

    var encoded = testCase.ToList();
    encoded.Should().BeEquivalentTo([x, y, z]);

    const string NEW_UNIT = "something different...";
    var decoded = Vector.FromList(encoded, NEW_UNIT);

    decoded.x.Should().Be(x);
    decoded.y.Should().Be(y);
    decoded.z.Should().Be(z);
    decoded.units.Should().Be(NEW_UNIT);
  }

  [Theory]
  [MemberData(nameof(TestCases))]
  public void ToPoint(double x, double y, double z, string units)
  {
    var testCase = new Vector(x, y, z, units, "asdfasdf");

    var asPoint = testCase.ToPoint();

    asPoint.x.Should().Be(x);
    asPoint.y.Should().Be(y);
    asPoint.z.Should().Be(z);
    asPoint.units.Should().Be(units);
    asPoint.applicationId.Should().Be("asdfasdf");
  }

  [Theory]
  [MemberData(nameof(TestCases))]
  public void Normalize(double x, double y, double z, string units)
  {
    var sut = new Vector(x, y, z, units);
    var originalLength = sut.Length;
    sut.Normalize();

    if (!(originalLength > 0))
    {
      sut.Length.Should().Be(double.NaN);
      return;
    }

    sut.Length.Should().BeApproximately(1, FLOAT_TOLERANCE);

    var rescaled = sut * originalLength;

    rescaled.x.Should().BeApproximately(x, FLOAT_TOLERANCE);
    rescaled.y.Should().BeApproximately(y, FLOAT_TOLERANCE);
    rescaled.z.Should().BeApproximately(z, FLOAT_TOLERANCE);
    rescaled.units.Should().Be(units);
  }

  [Theory]
  [MemberData(nameof(TestCases))]
  public void Negate(double x, double y, double z, string units)
  {
    var sut = new Vector(x, y, z, units);
    var originalLength = sut.Length;
    sut.Negate();

    sut.Length.Should().Be(originalLength);
    var rescaled = sut.Negate();

    rescaled.x.Should().BeApproximately(x, FLOAT_TOLERANCE);
    rescaled.y.Should().BeApproximately(y, FLOAT_TOLERANCE);
    rescaled.z.Should().BeApproximately(z, FLOAT_TOLERANCE);
    rescaled.units.Should().Be(units);
  }

  [Theory]
  [MemberData(nameof(TestCases))]
  public void TestAddition(double x, double y, double z, string units)
  {
    var operand1 = new Vector(x, y, z, units);
    var operand2 = new Vector(x, y, z, units);

    var result = operand1 + operand2;
    double[] expected = [x + x, y + y, z + z];

    result.ToList().Should().BeEquivalentTo(expected);
    result.units.Should().BeEquivalentTo(units);
  }

  [Theory]
  [MemberData(nameof(TestCases))]
  public void TestSubtraction(double x, double y, double z, string units)
  {
    var operand1 = new Vector(x, y, z, units);
    var operand2 = new Vector(x, y, z, units);

    var result = operand1 - operand2;
    double[] expected = [x - x, y - y, z - z];

    result.ToList().Should().BeEquivalentTo(expected);
    result.units.Should().BeEquivalentTo(units);
  }

  [Theory]
  [MemberData(nameof(TestCases))]
  public void TestDivision(double x, double y, double z, string units)
  {
    var operand1 = new Vector(x, y, z, units);
    const int OPERAND2 = 2;

    var result = operand1 / OPERAND2;
    double[] expected = [x / OPERAND2, y / OPERAND2, z / OPERAND2];

    result.ToList().Should().BeEquivalentTo(expected);
    result.units.Should().BeEquivalentTo(units);
  }

  [Theory]
  [MemberData(nameof(TestCases))]
  public void TestMultiplication(double x, double y, double z, string units)
  {
    var operand1 = new Vector(x, y, z, units);
    const int OPERAND2 = 2;

    var result = operand1 * OPERAND2;
    double[] expected = [x * OPERAND2, y * OPERAND2, z * OPERAND2];

    result.ToList().Should().BeEquivalentTo(expected);
    result.units.Should().BeEquivalentTo(units);
  }

  [Theory]
  [MemberData(nameof(TestCases))]
  public void TestDotProduct(double x, double y, double z, string units)
  {
    var operand1 = new Vector(x, y, z, units);
    var operand2 = new Vector(x, y, z, units);

    var result = Vector.DotProduct(operand1, operand2);
    double expected = Vector3.Dot(new Vector3(x, y, z), new Vector3(x, y, z));

    result.Should().BeApproximately(expected, FLOAT_TOLERANCE);
  }

  [Theory]
  [MemberData(nameof(TestCases))]
  public void TestCrossProduct(double x, double y, double z, string units)
  {
    var operand1 = new Vector(x, y, z, units);
    var operand2 = new Vector(x, y, z, units);

    var result = Vector.CrossProduct(operand1, operand2);
    var expected = Vector3.Cross(new Vector3(x, y, z), new Vector3(x, y, z));

    result.x.Should().BeApproximately(expected.X, FLOAT_TOLERANCE);
    result.y.Should().BeApproximately(expected.Y, FLOAT_TOLERANCE);
    result.z.Should().BeApproximately(expected.Z, FLOAT_TOLERANCE);
  }

  [Theory]
  [MemberData(nameof(TestCases))]
  [Obsolete("Tests Obsolete legacy behaviour to maintain backwards json compatibility with ~2.13? data")]
  public void TestLegacyValueProp(double x, double y, double z, string _)
  {
    var vector = Activator.CreateInstance<Vector>();
    vector.value = [x, y, z];

    vector.x.Should().Be(x);
    vector.y.Should().Be(y);
    vector.z.Should().Be(z);
  }
}
