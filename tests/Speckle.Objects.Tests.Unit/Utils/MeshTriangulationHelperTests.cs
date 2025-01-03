using Shouldly;
using Xunit;
using Speckle.Objects.Geometry;
using Speckle.Objects.Utils;
using Speckle.Sdk.Common;
using Speckle.Sdk.Dependencies;

namespace Speckle.Objects.Tests.Unit.Utils;

public class MeshTriangulationHelperTests
{
  public static IEnumerable<object[]> PolygonTestSource()
  {
    foreach (var x in EnumerableExtensions.RangeFrom(3,9))
    {
      yield return new object[] { x, true };
      yield return new object[] { x, false };
    }
  }
  [Theory]
  [MemberData(nameof(PolygonTestSource))]
  public void PolygonTest(int n, bool planar)
  {
    // Test Setup
    List<double> vertices = new(n) { 0, planar ? 0 : 1, 1 };
    for (int i = 1; i < n; i++)
    {
      vertices.Add(i);
      vertices.Add(0);
      vertices.Add(0);
    }

    List<int> faces = new(n + 1) { n };
    faces.AddRange(Enumerable.Range(0, n));

    Mesh mesh = new() { vertices = vertices, faces = faces, units = Units.Meters, };

    // Test
    mesh.TriangulateMesh();

    // Results
    int numExpectedTriangles = n - 2;
    int expectedFaceCount = numExpectedTriangles * 4;

    mesh.faces.Count.ShouldBe(expectedFaceCount);

    for (int i = 0; i < expectedFaceCount; i += 4)
    {
      mesh.faces[i].ShouldBe(3);
      mesh.faces.GetRange(i + 1, 3).ShouldBeUnique();
    }

    mesh.faces.ShouldAllBe(x => EnumerableExtensions.RangeFrom(0, n).Contains(x));
    mesh.faces.ShouldAllBe(f => f >= 0 && f < Math.Max(n, 4));
  }

  [Fact]
  public void DoesntFlipNormals()
  {
    // Test Setup
    List<double> vertices = new()
    {
      0,
      0,
      0,
      1,
      0,
      0,
      1,
      0,
      1
    };

    List<int> faces = new() { 3, 0, 1, 2 };

    Mesh mesh = new() { vertices = vertices, faces = new List<int>(faces), units = Units.Meters, };

    // Test
    mesh.TriangulateMesh();

    // Results
    List<int> shift1 = faces;
    List<int> shift2 = new() { 3, 1, 2, 0 };
    List<int> shift3 = new() { 3, 2, 0, 1 };

    new List<int>[] {shift1, shift2, shift3}.Any(x => mesh.faces.SequenceEqual(x)).ShouldBeTrue();
  }

  [Theory]
  [InlineData(true)]
  [InlineData(false)]
  public void PreserveQuads(bool preserveQuads)
  {
    // Test Setup
    List<double> vertices = new()
    {
      0,
      0,
      0,
      1,
      0,
      0,
      1,
      0,
      1,
      0,
      0,
      1
    };

    List<int> faces = new()
    {
      4,
      0,
      1,
      2,
      3
    };

    Mesh mesh = new() { vertices = vertices, faces = new List<int>(faces), units = Units.Meters, };

    // Tests
    mesh.TriangulateMesh(preserveQuads);

    // Results
    int expectedN = preserveQuads ? 4 : 3;
    int expectedFaceCount = preserveQuads ? 5 : 8;

    mesh.faces.Count.ShouldBe(expectedFaceCount);
    mesh.faces[0].ShouldBe(expectedN);
  }
}
