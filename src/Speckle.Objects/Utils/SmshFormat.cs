namespace Speckle.Objects.Utils;

/// <summary>
/// SMSH v1 binary format constants. MVP layout — raw float64 vertices and int32
/// faces; no quantization. The <c>Quantized</c> flag bit is reserved for a future
/// iteration that will reinterpret the vertex section as uint16 with per-mesh
/// origin + scale.
/// </summary>
/// <remarks>
/// Layout:
/// <code>
/// 0x00  4   magic "SMSH"  (0x48534D53 little-endian)
/// 0x04  2   version       (= 1)
/// 0x06  2   flags         (bit0 quantized, bit1 normals, bit2 uvs, bit3 colors)
/// 0x08  4   vertex_count  (uint32)
/// 0x0C  4   face_int_count (uint32, total ints in the legacy n-gon face stream)
/// 0x10  ─   vertices       (vertex_count × 3 × float64)
/// next  ─   faces          (face_int_count × int32)
/// next  ─   normals        (vertex_count × 3 × float64) if has_normals
/// next  ─   uvs            (vertex_count × 2 × float64) if has_uvs
/// next  ─   colors         (vertex_count × int32 ARGB)  if has_colors
/// </code>
/// </remarks>
internal static class SmshFormat
{
  public const uint Magic = 0x48534D53u; // "SMSH" little-endian
  public const ushort Version1 = 1;
  public const int HeaderSize = 16;
  public const string EncodingName = "smsh_v1";
}

[Flags]
internal enum SmshFlags : ushort
{
  None = 0,

  /// <summary>Reserved for the quantized layout — must be 0 in MVP.</summary>
  Quantized = 1 << 0,

  HasNormals = 1 << 1,
  HasUvs = 1 << 2,
  HasColors = 1 << 3,
}

internal struct SmshHeader
{
  public ushort Version;
  public SmshFlags Flags;
  public uint VertexCount;
  public uint FaceIntCount;
}

/// <summary>
/// Decoded SMSH buffer expanded back to flat arrays. Matches the shape of
/// <see cref="Speckle.Objects.Geometry.Mesh"/>'s fields one-for-one.
/// </summary>
public sealed class DecodedMesh
{
  public required double[] Vertices { get; init; }
  public required int[] Faces { get; init; }
  public double[]? Normals { get; init; }
  public double[]? TextureCoords { get; init; }
  public int[]? Colors { get; init; }
}
