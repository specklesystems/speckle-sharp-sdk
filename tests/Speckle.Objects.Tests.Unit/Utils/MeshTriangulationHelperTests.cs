using NUnit.Framework;
using Shouldly;
using Speckle.Objects.Geometry;
using Speckle.Objects.Utils;
using Speckle.Sdk.Common;
using Xunit;

namespace Speckle.Objects.Tests.Unit.Utils;

public class MeshTriangulationHelperTests
{
  public static IEnumerable<object[]> PolygonTestValues
  {
    get
    {

      for (int n = 3; n <= 9; n++)
      {
        yield return [n, true];
        yield return [n, false];
      }
    }
  }
  [Theory]
  [MemberData(nameof(PolygonTestValues))]
  public void PolygonTest(int n, bool planar)
  {
    //Test Setup
    List<double> vertices = new(n) { 0, planar ? 0 : 1, 1 };
    for (int i = 1; i < n; i++)
    {
      vertices.Add(i);
      vertices.Add(0);
      vertices.Add(0);
    }

    List<int> faces = new(n + 1) { n };
    faces.AddRange(Enumerable.Range(0, n));

    Mesh mesh =
      new()
      {
        vertices = vertices,
        faces = faces,
        units = Units.Meters,
      };

    //Test
    mesh.TriangulateMesh();

    //Results
    int numExpectedTriangles = n - 2;
    int expectedFaceCount = numExpectedTriangles * 4;

mesh.faces.Count.ShouldBe(expectedFaceCount);
    for (int i = 0; i < expectedFaceCount; i += 4)
    {
      mesh.faces[i].ShouldBe(3);
     // Assert.That(mesh.faces.GetRange(i + 1, 3), Is.Unique);
    }

   // Assert.That(mesh.faces, Is.SupersetOf(Enumerable.Range(0, n)));

    mesh.faces.ForEach(x => x.ShouldBeGreaterThanOrEqualTo(0));
    mesh.faces.ForEach(x => x.ShouldBeLessThan(Math.Max(n, 4)));
  }

  [Fact]
  public void DoesntFlipNormals()
  {
    //Test Setup
    List<double> vertices = new() { 0, 0, 0, 1, 0, 0, 1, 0, 1 };

    List<int> faces = new() { 3, 0, 1, 2 };

    Mesh mesh =
      new()
      {
        vertices = vertices,
        faces = new List<int>(faces),
        units = Units.Meters,
      };

    //Test
    mesh.TriangulateMesh();

    //Results

    List<int> shift1 = faces;
    List<int> shift2 = new() { 3, 1, 2, 0 };
    List<int> shift3 = new() { 3, 2, 0, 1 };

    mesh.faces.ShouldBeOneOf([shift1, shift2, shift3]);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public void PreserveQuads(bool preserveQuads)
  {
    //Test Setup
    List<double> vertices = new() { 0, 0, 0, 1, 0, 0, 1, 0, 1, 0, 0, 1 };

    List<int> faces = new() { 4, 0, 1, 2, 3 };

    Mesh mesh =
      new()
      {
        vertices = vertices,
        faces = new List<int>(faces),
        units = Units.Meters,
      };

    //Tests
    mesh.TriangulateMesh(preserveQuads);

    //Results
    int expectedN = preserveQuads ? 4 : 3;
    int expectedFaceCount = preserveQuads ? 5 : 8;

    mesh.faces.Count.ShouldBe(expectedFaceCount);
    mesh.faces[0].ShouldBe(expectedN);
  }
}
