using System.Buffers.Binary;
using Speckle.Objects.Geometry;
using Speckle.Sdk;
using Speckle.Sdk.Common;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Utils;

/// <summary>
/// Decodes SMSH v1 byte buffers back into either flat arrays or a legacy
/// <see cref="Mesh"/>. MVP: assumes the buffer is the unquantized layout
/// (vertices as raw float64, faces as int32).
/// </summary>
public static class MeshBinaryDecoder
{
  /// <summary>
  /// Decodes an SMSH buffer into flat arrays. Faces use the legacy n-gon
  /// format with cardinality prefixes (compatible with <see cref="Mesh"/>).
  /// </summary>
  public static DecodedMesh Decode(ReadOnlySpan<byte> bytes)
  {
    var header = ReadHeader(bytes);

    if ((header.Flags & SmshFlags.Quantized) != 0)
    {
      throw new SpeckleException(
        "Quantized SMSH layout is not yet supported by this decoder (reserved for a future iteration)."
      );
    }

    int vCount = (int)header.VertexCount;
    int fCount = (int)header.FaceIntCount;
    bool hasNormals = (header.Flags & SmshFlags.HasNormals) != 0;
    bool hasUvs = (header.Flags & SmshFlags.HasUvs) != 0;
    bool hasColors = (header.Flags & SmshFlags.HasColors) != 0;

    int offset = SmshFormat.HeaderSize;

    // Vertices
    var verts = new double[vCount * 3];
    for (int i = 0; i < verts.Length; i++)
    {
      verts[i] = MeshBinaryEncoder.ReadDoubleLE(bytes.Slice(offset, 8)); offset += 8;
    }

    // Faces
    var faces = new int[fCount];
    for (int i = 0; i < fCount; i++)
    {
      faces[i] = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset, 4)); offset += 4;
    }

    double[]? normals = null;
    if (hasNormals)
    {
      normals = new double[vCount * 3];
      for (int i = 0; i < normals.Length; i++)
      {
        normals[i] = MeshBinaryEncoder.ReadDoubleLE(bytes.Slice(offset, 8)); offset += 8;
      }
    }

    double[]? uvs = null;
    if (hasUvs)
    {
      uvs = new double[vCount * 2];
      for (int i = 0; i < uvs.Length; i++)
      {
        uvs[i] = MeshBinaryEncoder.ReadDoubleLE(bytes.Slice(offset, 8)); offset += 8;
      }
    }

    int[]? colors = null;
    if (hasColors)
    {
      colors = new int[vCount];
      for (int i = 0; i < vCount; i++)
      {
        colors[i] = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset, 4)); offset += 4;
      }
    }

    return new DecodedMesh
    {
      Vertices = verts,
      Faces = faces,
      Normals = normals,
      TextureCoords = uvs,
      Colors = colors,
    };
  }

  /// <summary>
  /// Decodes a <see cref="MeshBinary"/> into a legacy <see cref="Mesh"/> by
  /// reading the blob bytes from <paramref name="blobStorageFolder"/>.
  /// </summary>
  public static Mesh DecodeToLegacyMesh(MeshBinary source, string blobStorageFolder)
  {
    _ = source.NotNull();
    var bytes = source.geometryBlob.ReadAllBytes(blobStorageFolder);
    var decoded = Decode(bytes);
    return new Mesh
    {
      vertices = [.. decoded.Vertices],
      faces = [.. decoded.Faces],
      vertexNormals = decoded.Normals != null ? [.. decoded.Normals] : [],
      textureCoordinates = decoded.TextureCoords != null ? [.. decoded.TextureCoords] : [],
      colors = decoded.Colors != null ? [.. decoded.Colors] : [],
      units = source.units,
      area = source.area,
      volume = source.volume,
      bbox = source.bbox,
      applicationId = source.applicationId,
    };
  }

  /// <summary>
  /// Reads and validates the SMSH header without expanding any data sections.
  /// </summary>
  internal static SmshHeader ReadHeader(ReadOnlySpan<byte> bytes)
  {
    if (bytes.Length < SmshFormat.HeaderSize)
    {
      throw new SpeckleException("SMSH buffer too small to contain a header.");
    }
    uint magic = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(0, 4));
    if (magic != SmshFormat.Magic)
    {
      throw new SpeckleException($"SMSH magic mismatch: expected 0x{SmshFormat.Magic:X8}, got 0x{magic:X8}.");
    }
    ushort version = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(4, 2));
    if (version != SmshFormat.Version1)
    {
      throw new SpeckleException($"SMSH version {version} unsupported (this decoder reads {SmshFormat.Version1}).");
    }
    ushort flags = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(6, 2));
    uint vertCount = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(8, 4));
    uint faceIntCount = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(12, 4));

    return new SmshHeader
    {
      Version = version,
      Flags = (SmshFlags)flags,
      VertexCount = vertCount,
      FaceIntCount = faceIntCount,
    };
  }
}
