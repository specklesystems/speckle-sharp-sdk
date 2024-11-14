using NUnit.Framework;
using Shouldly;
using Speckle.Objects.Geometry;
using Speckle.Sdk.Common;
using Xunit;

namespace Speckle.Objects.Tests.Unit.Geometry;

public class MeshTests
{
  public static readonly IEnumerable<object[]> TestCaseSource = new object[][]
  {
    [CreateBlenderStylePolygon()],
    [CreateRhinoStylePolygon()],
  };

  [Theory, MemberData(nameof(TestCaseSource))]
  public void CanAlignVertices(Mesh inPolygon)
  {
    inPolygon.AlignVerticesWithTexCoordsByIndex();

    inPolygon.VerticesCount.ShouldBe(inPolygon.TextureCoordinatesCount);

    var expectedPolygon = CreateRhinoStylePolygon();

    inPolygon.vertices.ShouldBeEquivalentTo(expectedPolygon.vertices);
    inPolygon.faces.ShouldBeEquivalentTo(expectedPolygon.faces);
    inPolygon.textureCoordinates.ShouldBeEquivalentTo(expectedPolygon.textureCoordinates);
  }

  private static Mesh CreateRhinoStylePolygon()
  {
    return new Mesh
    {
      vertices = [0, 0, 0, 0, 0, 1, 1, 0, 1, 0, 0, 0, 1, 0, 1, 1, 0, 0],
      faces = [3, 0, 1, 2, 3, 3, 4, 5],
      textureCoordinates = [0, 0, 0, 1, 1, 1, 0, 0, 1, 1, 1, 0],
      units = Units.Meters,
    };
  }

  private static Mesh CreateBlenderStylePolygon()
  {
    return new Mesh
    {
      vertices = [0, 0, 0, 0, 0, 1, 1, 0, 1, 1, 0, 0],
      faces = [3, 0, 1, 2, 3, 0, 2, 3],
      textureCoordinates = [0, 0, 0, 1, 1, 1, 0, 0, 1, 1, 1, 0],
      units = Units.Meters,
    };
  }
}
