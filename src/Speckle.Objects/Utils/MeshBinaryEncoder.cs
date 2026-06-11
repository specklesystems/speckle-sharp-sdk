using System.Buffers.Binary;
using Speckle.Objects.Geometry;
using Speckle.Sdk.Common;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Utils;

/// <summary>
/// Encodes a legacy <see cref="Mesh"/> into a <see cref="MeshBinary"/> + SMSH v1
/// byte buffer. MVP: no quantization, no vertex dedup — vertex and face arrays
/// are dumped to binary as-is. Future iterations will quantize behind a flag.
/// </summary>
public static class MeshBinaryEncoder
{
  /// <summary>
  /// Encodes a legacy <see cref="Mesh"/> into a <see cref="MeshBinary"/>.
  /// The SMSH buffer is written to a file under <paramref name="tempDir"/>
  /// (defaults to <c>%TEMP%/Speckle</c>); the resulting <see cref="Blob"/>
  /// carries it through the transport layer.
  /// </summary>
  public static MeshBinary EncodeFromMesh(Mesh source, string? tempDir = null)
  {
    _ = source.NotNull();
    tempDir ??= Path.Combine(Path.GetTempPath(), "Speckle");

    var bytes = EncodeToSmsh(
      source.vertices.ToArray(),
      source.faces.ToArray(),
      source.vertexNormals.Count > 0 ? source.vertexNormals.ToArray() : [],
      source.textureCoordinates.Count > 0 ? source.textureCoordinates.ToArray() : [],
      source.colors.Count > 0 ? source.colors.ToArray() : []
    );

    var blob = BlobExtensions.FromBytes(bytes, tempDir);

    return new MeshBinary
    {
      units = source.units,
      encoding = SmshFormat.EncodingName,
      geometryBlob = blob,
      area = source.area,
      volume = source.volume,
      bbox = source.bbox,
      applicationId = source.applicationId,
    };
  }

  /// <summary>
  /// Encodes raw arrays into an SMSH v1 byte buffer. No file I/O.
  /// </summary>
  public static byte[] EncodeToSmsh(
    ReadOnlySpan<double> vertices,
    ReadOnlySpan<int> faces,
    ReadOnlySpan<double> normals,
    ReadOnlySpan<double> uvs,
    ReadOnlySpan<int> colors
  )
  {
    if (vertices.Length % 3 != 0)
    {
      throw new ArgumentException("vertices length must be a multiple of 3", nameof(vertices));
    }
    int vertCount = vertices.Length / 3;
    int faceIntCount = faces.Length;

    bool hasNormals = normals.Length > 0;
    bool hasUvs = uvs.Length > 0;
    bool hasColors = colors.Length > 0;

    if (hasNormals && normals.Length != vertices.Length)
    {
      throw new ArgumentException("normals length must equal vertices length when present", nameof(normals));
    }
    if (hasUvs && uvs.Length != vertCount * 2)
    {
      throw new ArgumentException("uvs length must be 2 × vertex count when present", nameof(uvs));
    }
    if (hasColors && colors.Length != vertCount)
    {
      throw new ArgumentException("colors length must equal vertex count when present", nameof(colors));
    }

    SmshFlags flags = SmshFlags.None;
    if (hasNormals) { flags |= SmshFlags.HasNormals; }
    if (hasUvs) { flags |= SmshFlags.HasUvs; }
    if (hasColors) { flags |= SmshFlags.HasColors; }

    int total = SmshFormat.HeaderSize
      + vertCount * 3 * 8       // vertices: 3 × float64
      + faceIntCount * 4;       // faces: int32
    if (hasNormals) { total += vertCount * 3 * 8; }
    if (hasUvs) { total += vertCount * 2 * 8; }
    if (hasColors) { total += vertCount * 4; }

    var buf = new byte[total];
    var span = buf.AsSpan();
    int offset = 0;

    // Header
    BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(offset, 4), SmshFormat.Magic); offset += 4;
    BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(offset, 2), SmshFormat.Version1); offset += 2;
    BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(offset, 2), (ushort)flags); offset += 2;
    BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(offset, 4), (uint)vertCount); offset += 4;
    BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(offset, 4), (uint)faceIntCount); offset += 4;

    // Vertices
    for (int i = 0; i < vertices.Length; i++)
    {
      WriteDoubleLE(span.Slice(offset, 8), vertices[i]); offset += 8;
    }

    // Faces (int32 little-endian)
    for (int i = 0; i < faceIntCount; i++)
    {
      BinaryPrimitives.WriteInt32LittleEndian(span.Slice(offset, 4), faces[i]); offset += 4;
    }

    if (hasNormals)
    {
      for (int i = 0; i < normals.Length; i++)
      {
        WriteDoubleLE(span.Slice(offset, 8), normals[i]); offset += 8;
      }
    }
    if (hasUvs)
    {
      for (int i = 0; i < uvs.Length; i++)
      {
        WriteDoubleLE(span.Slice(offset, 8), uvs[i]); offset += 8;
      }
    }
    if (hasColors)
    {
      for (int i = 0; i < colors.Length; i++)
      {
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(offset, 4), colors[i]); offset += 4;
      }
    }

    return buf;
  }

  // BinaryPrimitives.WriteDoubleLittleEndian doesn't exist on netstandard2.0,
  // so route through Int64Bits which is available everywhere.
  internal static void WriteDoubleLE(Span<byte> dst, double value)
  {
    long bits = BitConverter.DoubleToInt64Bits(value);
    BinaryPrimitives.WriteInt64LittleEndian(dst, bits);
  }

  internal static double ReadDoubleLE(ReadOnlySpan<byte> src)
  {
    long bits = BinaryPrimitives.ReadInt64LittleEndian(src);
    return BitConverter.Int64BitsToDouble(bits);
  }
}
