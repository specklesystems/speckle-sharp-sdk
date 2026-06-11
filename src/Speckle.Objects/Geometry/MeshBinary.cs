using Speckle.Objects.Other;
using Speckle.Objects.Utils;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Geometry;

/// <summary>
/// Mesh whose vertex and face data is stored as a binary buffer (SMSH v1)
/// carried by a detached <see cref="Blob"/>. The blob is content-addressed
/// (SHA256) so identical geometry across instances dedupes automatically at
/// the transport layer.
/// </summary>
/// <remarks>
/// MVP: the binary buffer holds raw float64 vertices and int32 faces — no
/// quantization, no per-mesh origin/scale. A future iteration will quantize
/// the vertex section behind a header flag without breaking this class's
/// public surface.
///
/// Use <c>MeshBinaryEncoder.EncodeFromMesh</c> to build one from a legacy
/// <see cref="Mesh"/> and <c>MeshBinaryDecoder.Decode</c> /
/// <c>DecodeToLegacyMesh</c> to read it back.
/// </remarks>
[SpeckleType("Objects.Geometry.MeshBinary")]
public class MeshBinary : Base, IHasBoundingBox, IHasVolume, IHasArea, ITransformable<MeshBinary>
{
  /// <summary>The units this mesh is in (e.g. <c>"m"</c>).</summary>
  public required string units { get; set; }

  /// <summary>Format identifier for the geometry blob. Currently always <c>"smsh_v1"</c>.</summary>
  public required string encoding { get; set; }

  /// <summary>
  /// The SMSH binary buffer containing vertices, faces, and any optional
  /// normals/UVs/colors. Stored as a detached <see cref="Blob"/> so its bytes
  /// travel as a separate, content-addressed attachment.
  /// </summary>
  [DetachProperty]
  public required Blob geometryBlob { get; set; }

  /// <inheritdoc/>
  public double area { get; set; }

  /// <inheritdoc/>
  public Box? bbox { get; set; }

  /// <inheritdoc/>
  public double volume { get; set; }

  /// <summary>
  /// Transforms this mesh by decoding its binary buffer to float64, applying
  /// <paramref name="transform"/> per-vertex, and re-encoding into a new SMSH
  /// blob written next to the source blob.
  /// </summary>
  /// <remarks>
  /// This is a slow path — it does a full decode + re-encode round-trip.
  /// For batch transforms, prefer decoding to a legacy <see cref="Mesh"/>,
  /// transforming there, and re-encoding once at the end.
  /// </remarks>
  public bool TransformTo(Transform transform, out MeshBinary transformed)
  {
    var srcDir = Path.GetDirectoryName(geometryBlob.filePath);
    if (string.IsNullOrEmpty(srcDir) || !File.Exists(geometryBlob.filePath))
    {
      transformed = this;
      return false;
    }

    var legacy = MeshBinaryDecoder.DecodeToLegacyMesh(this, srcDir);
    if (!legacy.TransformTo(transform, out Mesh legacyTransformed))
    {
      transformed = this;
      return false;
    }

    transformed = MeshBinaryEncoder.EncodeFromMesh(legacyTransformed, srcDir);
    return true;
  }

  /// <inheritdoc/>
  public bool TransformTo(Transform transform, out ITransformable transformed)
  {
    var ok = TransformTo(transform, out MeshBinary result);
    transformed = result;
    return ok;
  }
}
