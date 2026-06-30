namespace Speckle.Objects.Utils;

/// <summary>
/// SGEO v1 — Speckle's binary geometry family format. One opaque blob per
/// geometry buffer: a fixed 16-byte header followed by a per-primitive body.
/// The SDK owns this format; the viewer and connectors mirror the decode.
/// </summary>
/// <remarks>
/// Header (16 bytes, little-endian):
/// <code>
/// 0x00  4  magic           "SGEO"  (0x53 0x47 0x45 0x4F)
/// 0x04  1  version         = 1
/// 0x05  1  primitive_type  see PrimitiveType
/// 0x06  2  flags           uint16 (see SgeoFlags)
/// 0x08  2  units_code      uint16, Units.GetEncodingFromUnit
/// 0x0A  2  reserved        = 0
/// 0x0C  4  crc             CRC32 of body bytes only (0x10..end)
/// 0x10  …  body            per primitive_type
/// </code>
/// Conventions: little-endian; f64 = IEEE-754 double; the body starts 8-byte
/// aligned at 0x10 and every f64 array stays 8-aligned (u32 scalars are padded
/// in pairs). Derived fields (length/area/volume/bbox, arc radius/measure) are
/// not stored — they are recomputed on read. One unit per blob (header), so
/// composite sub-objects' own units are dropped. See
/// <c>plans/speckle-4.0/sgeo-binary-format.md</c> for the full spec.
/// </remarks>
public static class SgeoFormat
{
  /// <summary>The four magic bytes "SGEO" at offset 0x00.</summary>
  public static ReadOnlySpan<byte> Magic => "SGEO"u8;

  public const byte Version1 = 1;
  public const int HeaderSize = 16;
  public const string EncodingName = "sgeo_v1";
}

/// <summary>SGEO primitive type codes (header offset 0x05, stored as one byte).</summary>
public enum SgeoPrimitiveType
{
  Mesh = 0,
  Line = 1,
  Polyline = 2,
  Polycurve = 3,
  Curve = 4,
  Arc = 5,
  Circle = 6,
  Points = 7,
  Ellipse = 8,
  Spiral = 9,
  Box = 10,
}

/// <summary>SGEO header flags (offset 0x06, stored as a uint16 bitfield).</summary>
[Flags]
public enum SgeoFlags
{
  None = 0,

  /// <summary>Reserved for a future quantized layout — must be 0 in v1.</summary>
  Quantized = 1 << 0,

  /// <summary>polyline / curve / polycurve closed.</summary>
  Closed = 1 << 1,

  /// <summary>curve has non-uniform weights.</summary>
  Rational = 1 << 2,

  /// <summary>curve is periodic.</summary>
  Periodic = 1 << 3,

  /// <summary>mesh has vertex normals.</summary>
  HasNormals = 1 << 4,

  /// <summary>mesh has texture coordinates.</summary>
  HasUvs = 1 << 5,

  /// <summary>mesh / points has per-vertex colors.</summary>
  HasColors = 1 << 6,

  /// <summary>points (pointcloud) has per-point sizes.</summary>
  HasSizes = 1 << 7,

  /// <summary>ellipse has a trim domain.</summary>
  HasTrimDomain = 1 << 8,
}

/// <summary>The decoded SGEO header, without expanding the body.</summary>
public struct SgeoHeader
{
  public byte Version;
  public SgeoPrimitiveType PrimitiveType;
  public SgeoFlags Flags;
  public ushort UnitsCode;
  public uint Crc;
}

/// <summary>
/// CRC-32 (IEEE 802.3, canonical reflected polynomial 0xEDB88320) over a byte span.
/// Used as the SGEO header integrity check on the body bytes. This is the standard
/// CRC-32 (matches zlib.crc32 / System.IO.Hashing.Crc32) — the same polynomial the
/// Python (specklepy) and native (nw/rvextract) encoders use, so SGEO blobs stay
/// byte-for-byte identical across producers.
/// </summary>
internal static class Crc32
{
  private static readonly uint[] s_table = BuildTable();

  private static uint[] BuildTable()
  {
    var table = new uint[256];
    for (uint i = 0; i < 256; i++)
    {
      uint c = i;
      for (int k = 0; k < 8; k++)
      {
        c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
      }
      table[i] = c;
    }
    return table;
  }

  public static uint Compute(ReadOnlySpan<byte> bytes)
  {
    uint crc = 0xFFFFFFFFu;
    foreach (byte b in bytes)
    {
      crc = s_table[(crc ^ b) & 0xFF] ^ (crc >> 8);
    }
    return crc ^ 0xFFFFFFFFu;
  }
}
