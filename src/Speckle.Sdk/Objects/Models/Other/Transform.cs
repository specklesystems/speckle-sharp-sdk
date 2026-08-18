using Speckle.DoubleNumerics;
using Speckle.Sdk.Common;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Other;

/// <summary>
/// Generic transform class
/// </summary>
[SpeckleType("Objects.Other.Transform")]
public class Transform : Base
{
  /// <summary>
  /// The column-based 4x4 transform matrix
  /// </summary>
  /// <remarks>
  /// Graphics based apps typically use column-based matrices, where the last column defines translation.
  /// Modelling apps may use row-based matrices, where the last row defines translation. Transpose if so.
  /// </remarks>
  public required Matrix4x4 matrix { get; set; } = Matrix4x4.Identity;

  /// <summary>
  /// Units for translation
  /// </summary>
  public required string units { get; set; }

  /// <summary>
  /// Converts this transform to the input units
  /// </summary>
  /// <param name="newUnits">The target units</param>
  /// <returns>A matrix array with the translation scaled by input units</returns>
  /// <remarks>If either the transform's <see cref="units"/> or the given <paramref name="newUnits"/> is <see langword="null"/>, will return the matrix array data unscaled</remarks>
  public double[] ConvertToUnits(string newUnits)
  {
    if (newUnits == null || units == null)
    {
      return ToArray();
    }

    var sf = Units.GetConversionFactor(units, newUnits);

    return new[]
    {
      matrix.M11,
      matrix.M12,
      matrix.M13,
      matrix.M14 * sf,
      matrix.M21,
      matrix.M22,
      matrix.M23,
      matrix.M24 * sf,
      matrix.M31,
      matrix.M32,
      matrix.M33,
      matrix.M34 * sf,
      matrix.M41,
      matrix.M42,
      matrix.M43,
      matrix.M44,
    };
  }

  // Creates a matrix4x4 from a double array
  internal static Matrix4x4 CreateMatrix(double[] value)
  {
    return new Matrix4x4(
      value[0],
      value[1],
      value[2],
      value[3],
      value[4],
      value[5],
      value[6],
      value[7],
      value[8],
      value[9],
      value[10],
      value[11],
      value[12],
      value[13],
      value[14],
      value[15]
    );
  }

  // Creates a matrix from a float array
  internal static Matrix4x4 CreateMatrix(float[] value)
  {
    return new Matrix4x4(
      Convert.ToDouble(value[0]),
      Convert.ToDouble(value[1]),
      Convert.ToDouble(value[2]),
      Convert.ToDouble(value[3]),
      Convert.ToDouble(value[4]),
      Convert.ToDouble(value[5]),
      Convert.ToDouble(value[6]),
      Convert.ToDouble(value[7]),
      Convert.ToDouble(value[8]),
      Convert.ToDouble(value[9]),
      Convert.ToDouble(value[10]),
      Convert.ToDouble(value[11]),
      Convert.ToDouble(value[12]),
      Convert.ToDouble(value[13]),
      Convert.ToDouble(value[14]),
      Convert.ToDouble(value[15])
    );
  }

  /// <summary>
  /// Returns the double array of the transform matrix
  /// </summary>
  /// <returns></returns>
  public double[] ToArray()
  {
    return new double[]
    {
      matrix.M11,
      matrix.M12,
      matrix.M13,
      matrix.M14,
      matrix.M21,
      matrix.M22,
      matrix.M23,
      matrix.M24,
      matrix.M31,
      matrix.M32,
      matrix.M33,
      matrix.M34,
      matrix.M41,
      matrix.M42,
      matrix.M43,
      matrix.M44,
    };
  }
}
