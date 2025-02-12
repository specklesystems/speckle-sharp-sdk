using Speckle.Objects.Geometry;
using Speckle.Sdk.Common;
using Speckle.Sdk.Models;
using Point = Speckle.Objects.Geometry.Point;

namespace Speckle.Objects.SerializationTests;

public static class ObjectsTestData
{
  /// <summary>
  /// Data set of simple valid Speckle Objects
  /// Right now, we should have exactly one for each non-abstract object model.
  /// </summary>
  /// <returns></returns>
  private static IEnumerable<Base> Data()
  {
    yield return new Mesh()
    {
      vertices = [0d, 0d, 0d, 1d, 0d, 0d, 1d, 1d, 0d],
      faces = [3, 0, 1, 2],
      applicationId = "asdfasdf",
      units = Units.Meters,
      area = 42,
      volume = 420,
      colors = [-10185235, -10185235, -10185235],
    };
    yield return new Point()
    {
      x = 123.123,
      y = 111.222,
      z = -0.001,
      applicationId = "iosdf;juasdfioj",
      units = Units.Feet,
    };
    yield return new Vector()
    {
      x = 321.321,
      y = 222.111,
      z = -1.001,
      applicationId = "iosdf;juasdfioj",
      units = Units.Inches,
    };
  }

  public static TheoryData<Base> TheoryData => new(Data());
}
