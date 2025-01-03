using Shouldly;
using Speckle.Objects.Geometry;
using Speckle.Sdk.Common;
using Xunit;

namespace Speckle.Objects.Tests.Unit.Geometry;

public class MeshTests
{
  private static readonly Mesh[] TestCaseSource = { CreateBlenderStylePolygon(), CreateRhinoStylePolygon() };

  [Theory]
  [MemberData(nameof(GetTestCaseSource))]
  public void CanAlignVertices(Mesh inPolygon)
  {
    inPolygon.AlignVerticesWithTexCoordsByIndex();

    inPolygon.VerticesCount.ShouldBe(inPolygon.TextureCoordinatesCount);

    var expectedPolygon = CreateRhinoStylePolygon();

    inPolygon.vertices.ShouldBe(expectedPolygon.vertices);
    inPolygon.faces.ShouldBe(expectedPolygon.faces);
    inPolygon.textureCoordinates.ShouldBe(expectedPolygon.textureCoordinates);
  }

  public static IEnumerable<object[]> GetTestCaseSource() => TestCaseSource.Select(mesh => new object[] { mesh });

  private static Mesh CreateRhinoStylePolygon()
  {
    return new Mesh
    {
      vertices = new List<double> { 0, 0, 0, 0, 0, 1, 1, 0, 1, 0, 0, 0, 1, 0, 1, 1, 0, 0 },
      faces = new List<int> { 3, 0, 1, 2, 3, 3, 4, 5 },
      textureCoordinates = new List<double> { 0, 0, 0, 1, 1, 1, 0, 0, 1, 1, 1, 0 },
      units = Units.Meters,
    };
  }

  private static Mesh CreateBlenderStylePolygon()
  {
    return new Mesh
    {
      vertices = new List<double> { 0, 0, 0, 0, 0, 1, 1, 0, 1, 1, 0, 0 },
      faces = new List<int> { 3, 0, 1, 2, 3, 0, 2, 3 },
      textureCoordinates = new List<double> { 0, 0, 0, 1, 1, 1, 0, 0, 1, 1, 1, 0 },
      units = Units.Meters,
    };
  }
}
