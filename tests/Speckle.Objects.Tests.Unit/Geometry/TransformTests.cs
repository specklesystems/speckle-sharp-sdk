using Shouldly;
using Speckle.DoubleNumerics;
using Speckle.Objects.Other;
using Speckle.Sdk.Common;
using Xunit;

namespace Speckle.Objects.Tests.Unit.Geometry;

public class TransformTests
{
  private const float FLOAT_TOLERANCE = 1e-6f;

  [Theory]
  [MemberData(nameof(TransformTestCases))]
  public void ArrayBackAndForth(Matrix4x4 data)
  {
    // Arrange
    var start = new Transform() { matrix = data, units = Units.None };

    // Act
    var asArr = Transform.CreateMatrix(start.ToArray());
    var end = new Transform() { matrix = asArr, units = Units.None };

    // Assert
    end.matrix.ShouldBe(data);
  }

  [Theory]
  [MemberData(nameof(TransformTestCases))]
  public void ConvertToUnits(Matrix4x4 data)
  {
    const float SF = 1000f;

    // Arrange
    var transpose = Matrix4x4.Transpose(data); // Transform expects matrices transposed (translation in column 4)
    var mm = Matrix4x4.Transpose(
      Transform.CreateMatrix(
        new Transform() { matrix = transpose, units = Units.Meters }.ConvertToUnits(Units.Millimeters)
      )
    );

    // Act
    Matrix4x4.Decompose(data, out var ms, out var mr, out var mt);
    Matrix4x4.Decompose(mm, out var mms, out var mmr, out var mmt);

    // Assert
    mms.X.ShouldBe(ms.X, FLOAT_TOLERANCE, "Expect scale x to be unchanged");
    mms.Y.ShouldBe(ms.Y, FLOAT_TOLERANCE, "Expect scale y to be unchanged");
    mms.Z.ShouldBe(ms.Z, FLOAT_TOLERANCE, "Expect scale z to be unchanged");

    Quaternion.Dot(mr, mmr).ShouldBeLessThan(1 + FLOAT_TOLERANCE, "Expect rotation to be equivalent");

    mmt.X.ShouldBe(mt.X * SF, FLOAT_TOLERANCE, $"Expect translation x to be scaled by {SF}");
    mmt.Y.ShouldBe(mt.Y * SF, FLOAT_TOLERANCE, $"Expect translation y to be scaled by {SF}");
    mmt.Z.ShouldBe(mt.Z * SF, FLOAT_TOLERANCE, $"Expect translation z to be scaled by {SF}");
  }

  /// <summary>
  /// Set of TRS transforms (row dominant i.e. translation in row 4)
  /// All with non-negative scale and rotation (for ease of testing scale and rot independently)
  /// </summary>
  public static IEnumerable<object[]> TransformTestCases()
  {
    var t = new Vector3(128.128f, 255.255f, 512.512f);
    var r = Quaternion.CreateFromYawPitchRoll(1.9f, 0.6666667f, 0.5f);
    var s = new Vector3(123f, 32f, 0.5f);

    yield return [Matrix4x4.Identity];

    yield return [Matrix4x4.CreateTranslation(t)];

    yield return [Matrix4x4.CreateTranslation(t * -Vector3.UnitX)];

    yield return [Matrix4x4.CreateTranslation(t * -Vector3.UnitY)];

    yield return [Matrix4x4.CreateTranslation(t * -Vector3.UnitZ)];

    yield return [Matrix4x4.CreateTranslation(-t)];

    yield return [Matrix4x4.CreateFromYawPitchRoll(0.5f, 0.0f, 0.0f)];

    yield return [Matrix4x4.CreateFromYawPitchRoll(0.0f, 0.5f, 0.0f)];

    yield return [Matrix4x4.CreateFromYawPitchRoll(0.0f, 0.0f, 0.5f)];

    yield return [Matrix4x4.CreateFromYawPitchRoll(0.5f, 0.5f, 0.5f)];

    yield return [Matrix4x4.CreateFromQuaternion(r)];

    yield return [Matrix4x4.Identity + Matrix4x4.CreateScale(s)];

    yield return [Matrix4x4.CreateTranslation(t) + Matrix4x4.CreateFromQuaternion(r)];

    yield return [Matrix4x4.CreateTranslation(t) + Matrix4x4.CreateFromQuaternion(r) + Matrix4x4.CreateScale(s)];

    yield return [Matrix4x4.CreateTranslation(t) + Matrix4x4.CreateFromQuaternion(r) + Matrix4x4.CreateScale(-s)];
  }
}
