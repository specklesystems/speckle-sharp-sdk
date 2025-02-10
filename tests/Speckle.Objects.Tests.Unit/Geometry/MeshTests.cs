using FluentAssertions;
using Speckle.Objects.Geometry;
using Speckle.Sdk.Common;

namespace Speckle.Objects.Tests.Unit.Geometry;

public class MeshTests
{
  private static readonly Mesh[] TestCaseSource = { CreateRhinoStylePolygon(), CreateEmpty() };

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

  private static Mesh CreateEmpty()
  {
    return new Mesh
    {
      vertices = [],
      faces = [],
      textureCoordinates = [],
      units = Units.Meters,
    };
  }

  [Theory]
  [MemberData(nameof(GetTestCaseSource))]
  public void GetTextureCoordinate_ReturnsCorrectUVValue(Mesh testCase)
  {
    for (int i = 0, j = 0; i < testCase.textureCoordinates.Count; i += 2, j++)
    {
      var (u, v) = testCase.GetTextureCoordinate(j);

      u.Should().Be(testCase.textureCoordinates[i]);
      v.Should().Be(testCase.textureCoordinates[i + 1]);
    }

    Assert.Throws<ArgumentOutOfRangeException>(() => testCase.GetTextureCoordinate(testCase.textureCoordinates.Count));
  }

  [Theory]
  [MemberData(nameof(GetTestCaseSource))]
  public void GetPoints_ReturnsVerticesAsPoints(Mesh testCase)
  {
    testCase.VerticesCount.Should().Be(testCase.vertices.Count / 3);

    var getPoints = testCase.GetPoints();
    var getPoint = Enumerable.Range(0, testCase.VerticesCount).Select(i => testCase.GetPoint(i));

    //Test each point has correct units
    getPoints.Select(x => x.units).Should().AllBe(testCase.units).And.HaveCount(testCase.VerticesCount);
    getPoints.Select(x => x.units).Should().AllBe(testCase.units).And.HaveCount(testCase.VerticesCount);

    //Convert back to flat list
    var expected = testCase.vertices;
    var getPointsList = getPoints.SelectMany(x => x.ToList());
    var getPointList = getPoint.SelectMany(x => x.ToList());

    getPointsList.Should().BeEquivalentTo(expected);
    getPointList.Should().BeEquivalentTo(expected);
  }
}
