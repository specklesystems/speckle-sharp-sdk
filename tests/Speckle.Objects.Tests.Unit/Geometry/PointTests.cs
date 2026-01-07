using System.Diagnostics.CodeAnalysis;
using AwesomeAssertions;
using Speckle.DoubleNumerics;
using Speckle.Objects.Geometry;
using Speckle.Objects.Other;
using Speckle.Sdk.Common;

namespace Speckle.Objects.Tests.Unit.Geometry;

public class PointTests
{
  [Fact]
  [SuppressMessage(
    "Assertion",
    "xUnit2013:Do not use equality check to assert boolean value",
    Justification = "Explicit equality operator tests are necessary"
  )]
  public void TestNull()
  {
    Point? a = null;
    Point? b = null;
    Point c = new(0, 0, 0, Units.Meters);

    a.Should().Be(b);
    (a != b).Should().BeFalse();

    b.Should().Be(a);
    (b != a).Should().BeFalse();

    (a == c).Should().BeFalse();
    (a != c).Should().BeTrue();

    (c == a).Should().BeFalse();
    (c != a).Should().BeTrue();
  }

  //TODO: Should(). units be allowed to be string?
  [Theory]
  [InlineData(1, 1, 1, "m", 1, 1, 1, "m", true)]
  [InlineData(1, 1, 1, "m", 0, 1, 1, "m", false)]
  [InlineData(1, 1, 1, "m", 1, 0, 1, "m", false)]
  [InlineData(1, 1, 1, "m", 1, 1, 0, "m", false)]
  [InlineData(1, 1, 1, "", 1, 1, 1, "", false)]
  [InlineData(1, 1, 1, null, 1, 1, 1, null, false)]
  [InlineData(1, 1, 1, "m", 1, 1, 1, "meters", false)]
  [InlineData(1, 1, 1, "m", 1, 1, 1, "M", false)]
  public void TestEqual(
    double x1,
    double y1,
    double z1,
    string? units1,
    double x2,
    double y2,
    double z2,
    string? units2,
    bool expectedResult
  )
  {
    if (string.IsNullOrEmpty(units1) || string.IsNullOrEmpty(units2))
    {
      expectedResult.Should().BeFalse();
      return;
    }
    Point p1 = new(x1, y1, z1, units1);
    Point p2 = new(x2, y2, z2, units2);

    (p1 == p2).Should().Be(expectedResult);
  }

  [Fact]
  public void TestDistanceTo()
  {
    //Arrange
    var p1 = new Point(1, 0, 0, units: Units.Meters);
    var p2 = new Point(0, 0, 0, units: Units.Meters);

    //Act
    var result = p1.DistanceTo(p2);

    //Assert
    result.Should().Be(1);
  }

  private static IReadOnlyList<Matrix4x4> MatrixTestData =>
    [
      Matrix4x4.Identity,
      Matrix4x4.CreateScale(1, 2, 3),
      Matrix4x4.CreateTranslation(100, 10, 0),
      Matrix4x4.CreateRotationZ(1),
    ];

  private static IReadOnlyList<Point> PointTestData =>
    [
      new(1, 2, 3, Units.Meters),
      new(0.5, 100.5, 123.123, Units.Meters),
      new(1, 2, 3, Units.Meters, applicationId: "Test me!"),
      new(0, 0, 0, Units.Feet),
    ];

  public static TheoryData<Point> PointTestCases() => new(PointTestData);

  public static TheoryData<Matrix4x4, Point> TransformTestCases()
  {
    TheoryData<Matrix4x4, Point> testCases = new();
    for (int i = 0; i < PointTestData.Count; i++)
    {
      testCases.Add(MatrixTestData[i], PointTestData[i]);
    }
    return testCases;
  }

  [Theory]
  [MemberData(nameof(TransformTestCases))]
  public void TransformPoint_SameUnits(Matrix4x4 matrix, Point point)
  {
    //Arrange
    Transform t = new() { matrix = Matrix4x4.Transpose(matrix), units = point.units };

    Vector3 expectedVector = Vector3.Transform(new(point.x, point.y, point.z), matrix);
    var expectedResult = (expectedVector.X, expectedVector.Y, expectedVector.Z);

    //Act
    point.TransformTo(t, out Point transformedPoint);
    var actualResult = (transformedPoint.x, transformedPoint.y, transformedPoint.z);

    //Assert
    actualResult.Should().Be(expectedResult);
    transformedPoint.applicationId.Should().Be(point.applicationId);

    transformedPoint.applicationId.Should().Be(point.applicationId);
    transformedPoint.id.Should().Be(point.id);
    transformedPoint.units.Should().Be(point.units);
  }

  [Fact(Skip = "Something clearly wrong with units!!!")]
  public void TransformingPoint_ChangeOfUnits()
  {
    //Arrange
    Point point = new(0, 0, 10, Units.Meters);
    Transform t = new()
    {
      matrix = Matrix4x4.Transpose(Matrix4x4.CreateTranslation(1000, 0, 0)),
      units = Units.Millimeters,
    };
    Vector3 expected = new(1, 0, 10);

    //Act
    point.TransformTo(t, out Point transformedPoint);

    //Assert
    (double x, double y, double z) = transformedPoint;
    transformedPoint.units.Should().Be(point.units);
    transformedPoint.applicationId.Should().Be(point.applicationId);

    new Vector3(x, y, z).Should().Be(expected);
  }

  [Theory]
  [MemberData(nameof(PointTestCases))]
  public void ToVector(Point testCase)
  {
    var expectedXYZ = (testCase.x, testCase.y, testCase.z);
    var expectedUnits = testCase.units;
    var expectedApplicationId = testCase.applicationId;

    var asVector = testCase.ToVector();
    var resultXYZ = (asVector.x, asVector.y, asVector.z);

    resultXYZ.Should().Be(expectedXYZ);
    asVector.units.Should().Be(expectedUnits);
    asVector.applicationId.Should().Be(expectedApplicationId);
  }

  [Theory]
  [MemberData(nameof(PointTestCases))]
  public void Deconstruct_Double_Double_Double_String(Point testCase)
  {
    (double x, double y, double z, string? units) = testCase;

    x.Should().Be(testCase.x);
    y.Should().Be(testCase.y);
    z.Should().Be(testCase.z);
    units.Should().Be(testCase.units);
  }

  [Theory]
  [MemberData(nameof(PointTestCases))]
  public void Deconstruct_Double_Double_Double(Point testCase)
  {
    (double x, double y, double z) = testCase;

    x.Should().Be(testCase.x);
    y.Should().Be(testCase.y);
    z.Should().Be(testCase.z);
  }
}
