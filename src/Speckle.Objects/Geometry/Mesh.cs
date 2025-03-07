using System.Diagnostics.Contracts;
using Speckle.Newtonsoft.Json;
using Speckle.Objects.Other;
using Speckle.Objects.Utils;
using Speckle.Sdk;
using Speckle.Sdk.Common;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Geometry;

/// <remarks><a href="https://speckle.notion.site/Objects-Geometry-Mesh-9b0bf5ab92bf42f58bf2fe3922d2efca">More docs on notion</a></remarks>
[SpeckleType("Objects.Geometry.Mesh")]
public class Mesh : Base, IHasBoundingBox, IHasVolume, IHasArea, ITransformable<Mesh>
{
  /// <summary>
  /// Flat list of vertex data (flat <c>x,y,z,x,y,z...</c> list)
  /// </summary>
  [DetachProperty, Chunkable(31250)]
  public required List<double> vertices { get; set; }

  /// <summary>
  /// Flat list of face data<br/>
  /// Each face starts with the length of the face (e.g. 3 in the case of triangles), followed by that many indices
  /// </summary>
  /// <remarks>
  /// N-gons are supported, but large values of n (> ~50) tend to cause significant performance problems for consumers (e.g. HostApps and <see cref="MeshTriangulationHelper"/>.
  /// </remarks>
  /// <example>
  /// <code>[
  ///   3, 0, 1, 2, //first face, a triangle (3-gon)
  ///   4, 1, 2, 3, 4, //second face, a quad (4-gon)
  ///   6, 4, 5, 6, 7, 8, 9, //third face, an n-gon (6-gon)
  /// ];</code></example>
  [DetachProperty, Chunkable(62500)]
  public required List<int> faces { get; set; }

  /// <summary>Vertex colors as ARGB <see cref="int"/>s</summary>
  /// <remarks>Expected that there are either 1 color per vertex, or an empty <see cref="List{T}"/></remarks>
  [DetachProperty, Chunkable(62500)]
  public List<int> colors { get; set; } = new();

  /// <summary>Flat list of texture coordinates (flat <c>u,v,u,v,u,v...</c> list)</summary>
  /// <remarks>Expected that there are either 1 texture coordinate per vertex, or an empty <see cref="List{T}"/></remarks>
  [DetachProperty, Chunkable(31250)]
  public List<double> textureCoordinates { get; set; } = new();

  /// <summary>
  /// <summary>Flat list of vertex normal data (flat <c>x,y,z,x,y,z...</c> list)</summary>
  /// <remarks>Expected that there are either 1 texture coordinate per vertex, or an empty <see cref="List{T}"/></remarks>
  /// </summary>
  [DetachProperty, Chunkable(31250)]
  public List<double> vertexNormals { get; set; } = new();

  /// <summary>
  /// The unit's this <see cref="Mesh"/> is in.
  /// This should be one of <see cref="Units"/>
  /// </summary>
  public required string units { get; set; }

  /// <inheritdoc/>
  public double area { get; set; }

  /// <inheritdoc/>
  public Box? bbox { get; set; }

  /// <inheritdoc/>
  public double volume { get; set; }

  /// <inheritdoc/>
  public bool Transform(Transform transform)
  {
    // transform vertices
    vertices = GetPoints()
      .SelectMany(vertex =>
      {
        vertex.TransformTo(transform, out Point transformedVertex);
        return transformedVertex.ToList();
      })
      .ToList();

    return true;
  }

  /// <inheritdoc/>
  public bool TransformTo(Transform transform, out Mesh transformed)
  {
    // transform vertices
    var transformedVertices = new List<Point>();
    foreach (var vertex in GetPoints())
    {
      vertex.TransformTo(transform, out Point transformedVertex);
      transformedVertices.Add(transformedVertex);
    }

    transformed = new Mesh
    {
      vertices = transformedVertices.SelectMany(o => o.ToList()).ToList(),
      textureCoordinates = textureCoordinates,
      applicationId = applicationId ?? id,
      faces = faces,
      colors = colors,
      units = units,
    };
    transformed["renderMaterial"] = this["renderMaterial"];

    return true;
  }

  /// <inheritdoc/>
  public bool TransformTo(Transform transform, out ITransformable transformed)
  {
    var res = TransformTo(transform, out Mesh brep);
    transformed = brep;
    return res;
  }

  [JsonIgnore]
  public int VerticesCount => vertices.Count / 3;

  [JsonIgnore]
  public int TextureCoordinatesCount => textureCoordinates.Count / 2;

  /// <summary>
  /// Gets a vertex as a <see cref="Point"/> by <paramref name="index"/>
  /// </summary>
  /// <param name="index">The index of the vertex</param>
  /// <returns>Vertex as a <see cref="Point"/></returns>
  /// <remarks>It is usually recommended to instead consume the <see cref="vertices"/> list manually for better performance</remarks>
  [Pure]
  public Point GetPoint(int index)
  {
    index *= 3;
    return new Point(vertices[index], vertices[index + 1], vertices[index + 2], units, applicationId);
  }

  /// <returns><see cref="vertices"/> as list of <see cref="Point"/>s</returns>
  /// <exception cref="SpeckleException">when list is malformed</exception>
  /// <remarks>It is usually recommended to instead consume the <see cref="vertices"/> list manually for better performance</remarks>
  [Pure]
  public List<Point> GetPoints()
  {
    if (vertices.Count % 3 != 0)
    {
      throw new SpeckleException(
        $"{nameof(Mesh)}.{nameof(vertices)} list is malformed: expected length to be multiple of 3"
      );
    }

    var pts = new List<Point>(vertices.Count / 3);
    for (int i = 2; i < vertices.Count; i += 3)
    {
      pts.Add(new Point(vertices[i - 2], vertices[i - 1], vertices[i], units));
    }

    return pts;
  }

  /// <summary>
  /// Gets a texture coordinate as a <see cref="ValueTuple{T1, T2}"/> by <paramref name="index"/>
  /// </summary>
  /// <param name="index">The index of the texture coordinate</param>
  /// <returns>Texture coordinate as a <see cref="ValueTuple{T1, T2}"/></returns>
  [Pure]
  public (double, double) GetTextureCoordinate(int index)
  {
    index *= 2;
    return (textureCoordinates[index], textureCoordinates[index + 1]);
  }
}
