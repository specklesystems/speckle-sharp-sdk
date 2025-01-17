using Speckle.Newtonsoft.Json;
using Speckle.Objects.Other;
using Speckle.Sdk;
using Speckle.Sdk.Common;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Geometry;

[SpeckleType("Objects.Geometry.Mesh")]
public class Mesh : Base, IHasBoundingBox, IHasVolume, IHasArea, ITransformable<Mesh>
{
  [DetachProperty, Chunkable(31250)]
  public required List<double> vertices { get; set; }

  [DetachProperty, Chunkable(62500)]
  public required List<int> faces { get; set; }

  /// <summary> Vertex colors as ARGB <see cref="int"/>s</summary>
  [DetachProperty, Chunkable(62500)]
  public List<int> colors { get; set; } = new();

  [DetachProperty, Chunkable(31250)]
  public List<double> textureCoordinates { get; set; } = new();

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
        var transformedVertex = vertex.TransformTo(transform);
        return transformedVertex.ToList();
      })
      .ToList();

    return true;
  }

  /// <inheritdoc/>
  public Mesh TransformTo(Transform transform)
  {
    // transform vertices
    var originalVertices = GetPoints();
    var transformedVertices = new List<double>(originalVertices.Count * 3);
    foreach (var vertex in originalVertices)
    {
      Point transformedVertex = vertex.TransformTo(transform);
      transformedVertices.Add(transformedVertex.x);
      transformedVertices.Add(transformedVertex.y);
      transformedVertices.Add(transformedVertex.z);
    }

    return new Mesh
    {
      vertices = transformedVertices,
      textureCoordinates = textureCoordinates,
      applicationId = applicationId ?? id,
      faces = faces,
      colors = colors,
      units = units,
      ["renderMaterial"] = this["renderMaterial"],
    };
  }

  #region Convenience Methods

  [JsonIgnore]
  public int VerticesCount => vertices.Count / 3;

  [JsonIgnore]
  public int TextureCoordinatesCount => textureCoordinates.Count / 2;

  /// <summary>
  /// Gets a vertex as a <see cref="Point"/> by <paramref name="index"/>
  /// </summary>
  /// <param name="index">The index of the vertex</param>
  /// <returns>Vertex as a <see cref="Point"/></returns>
  public Point GetPoint(int index)
  {
    index *= 3;
    return new Point(vertices[index], vertices[index + 1], vertices[index + 2], units, applicationId);
  }

  /// <returns><see cref="vertices"/> as list of <see cref="Point"/>s</returns>
  /// <exception cref="SpeckleException">when list is malformed</exception>
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
  public (double, double) GetTextureCoordinate(int index)
  {
    index *= 2;
    return (textureCoordinates[index], textureCoordinates[index + 1]);
  }

  /// <summary>
  /// If not already so, this method will align <see cref="Mesh.vertices"/>
  /// such that a vertex and its corresponding texture coordinates have the same index.
  /// This alignment is what is expected by most applications.<br/>
  /// </summary>
  /// <remarks>
  /// If the calling application expects
  /// <code>vertices.count == textureCoordinates.count</code>
  /// Then this method should be called by the <c>MeshToNative</c> method before parsing <see cref="Mesh.vertices"/> and <see cref="Mesh.faces"/>
  /// to ensure compatibility with geometry originating from applications that map <see cref="Mesh.vertices"/> to <see cref="Mesh.textureCoordinates"/> using vertex instance index (rather than vertex index)
  /// <br/>
  /// <see cref="Mesh.vertices"/>, <see cref="Mesh.colors"/>, and <see cref="faces"/> lists will be modified to contain no shared vertices (vertices shared between polygons)
  /// </remarks>
  public void AlignVerticesWithTexCoordsByIndex()
  {
    if (textureCoordinates.Count == 0)
    {
      return;
    }

    if (TextureCoordinatesCount == VerticesCount)
    {
      return; //Tex-coords already aligned as expected
    }

    var facesUnique = new List<int>(faces.Count);
    var verticesUnique = new List<double>(TextureCoordinatesCount * 3);
    bool hasColors = colors.Count > 0;
    var colorsUnique = hasColors ? new List<int>(TextureCoordinatesCount) : null;

    int nIndex = 0;
    while (nIndex < faces.Count)
    {
      int n = faces[nIndex];
      if (n < 3)
      {
        n += 3; // 0 -> 3, 1 -> 4
      }

      if (nIndex + n >= faces.Count)
      {
        break; //Malformed face list
      }

      facesUnique.Add(n);
      for (int i = 1; i <= n; i++)
      {
        int vertIndex = faces[nIndex + i];
        int newVertIndex = verticesUnique.Count / 3;

        var (x, y, z) = GetPoint(vertIndex);
        verticesUnique.Add(x);
        verticesUnique.Add(y);
        verticesUnique.Add(z);

        colorsUnique?.Add(colors[vertIndex]);
        facesUnique.Add(newVertIndex);
      }

      nIndex += n + 1;
    }

    vertices = verticesUnique;
    colors = colorsUnique ?? colors;
    faces = facesUnique;
  }

  #endregion
}
