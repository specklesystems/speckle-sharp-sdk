using System.Buffers.Binary;
using Speckle.Objects.Geometry;
using Speckle.Objects.Primitive;
using Speckle.Sdk;
using Speckle.Sdk.Common;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Utils;

/// <summary>Base-free decoded mesh fields: flat xyz <c>Vertices</c>, Speckle-format <c>Faces</c> (count-prefixed per
/// face), per-vertex argb <c>Colors</c> (may be empty) and <c>Units</c>. For hosts that build their own mesh type
/// directly from a SGEO blob without allocating a <see cref="Geometry.Mesh"/> (Base). See <see cref="SgeoDecoder.TryDecodeMesh"/>.</summary>
#pragma warning disable CA1819 // flat geometry arrays are intentional; this is a lightweight transport record
public readonly record struct SgeoMesh(double[] Vertices, int[] Faces, int[] Colors, string Units);
#pragma warning restore CA1819

/// <summary>
/// Decodes SGEO v1 byte buffers (see <see cref="SgeoFormat"/>) back into Speckle
/// geometry objects. The inverse of <see cref="SgeoEncoder"/>; derived fields
/// (length/area/volume/bbox, arc radius/measure) are recomputed by the objects.
/// </summary>
public static class SgeoDecoder
{
  /// <summary>
  /// Reads and validates the SGEO header (magic, version, CRC over the body)
  /// without expanding the body.
  /// </summary>
  public static SgeoHeader ReadHeader(ReadOnlySpan<byte> bytes)
  {
    if (bytes.Length < SgeoFormat.HeaderSize)
    {
      throw new SpeckleException("SGEO buffer too small to contain a header.");
    }
    if (!bytes.Slice(0, 4).SequenceEqual(SgeoFormat.Magic))
    {
      throw new SpeckleException("SGEO magic mismatch: expected \"SGEO\".");
    }
    byte version = bytes[4];
    if (version != SgeoFormat.Version1)
    {
      throw new SpeckleException($"SGEO version {version} unsupported (this decoder reads {SgeoFormat.Version1}).");
    }
    var primitiveType = (SgeoPrimitiveType)bytes[5];
    var flags = (SgeoFlags)BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(6, 2));
    ushort unitsCode = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(8, 2));
    uint crc = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(12, 4));

    uint actual = Crc32.Compute(bytes.Slice(SgeoFormat.HeaderSize));
    if (actual != crc)
    {
      throw new SpeckleException($"SGEO CRC mismatch: header 0x{crc:X8}, computed 0x{actual:X8}.");
    }

    return new SgeoHeader
    {
      Version = version,
      PrimitiveType = primitiveType,
      Flags = flags,
      UnitsCode = unitsCode,
      Crc = crc,
    };
  }

  /// <summary>Decodes a SGEO <see cref="SgeoPrimitiveType.Mesh"/> blob to neutral mesh fields without allocating a
  /// <see cref="Mesh"/> (Base). Returns false for non-mesh primitives or the (unsupported) quantized layout.</summary>
  public static bool TryDecodeMesh(ReadOnlySpan<byte> bytes, out SgeoMesh mesh)
  {
    mesh = default;
    var header = ReadHeader(bytes);
    if (header.PrimitiveType != SgeoPrimitiveType.Mesh || (header.Flags & SgeoFlags.Quantized) != 0)
    {
      return false;
    }
    string units = Units.GetUnitFromEncoding(header.UnitsCode);
    var r = new Reader(bytes, SgeoFormat.HeaderSize);
    int vCount = (int)r.U();
    int fCount = (int)r.U();
    var verts = new double[vCount * 3];
    for (int i = 0; i < verts.Length; i++)
    {
      verts[i] = r.D();
    }
    var faces = new int[fCount];
    for (int i = 0; i < fCount; i++)
    {
      faces[i] = r.I();
    }
    // skip normals + uvs (same order as Decode) so the colour cursor lands correctly.
    if ((header.Flags & SgeoFlags.HasNormals) != 0)
    {
      r.Align8();
      for (int i = 0; i < vCount * 3; i++)
      {
        r.D();
      }
    }
    if ((header.Flags & SgeoFlags.HasUvs) != 0)
    {
      r.Align8();
      for (int i = 0; i < vCount * 2; i++)
      {
        r.D();
      }
    }
    int[] colors = Array.Empty<int>();
    if ((header.Flags & SgeoFlags.HasColors) != 0)
    {
      colors = new int[vCount];
      for (int i = 0; i < vCount; i++)
      {
        colors[i] = r.I();
      }
    }
    mesh = new SgeoMesh(verts, faces, colors, units);
    return true;
  }

  /// <summary>Decodes an SGEO blob into the corresponding Speckle geometry object.</summary>
  /// <remarks>
  /// A single <see cref="Point"/> is encoded under the Points primitive and decodes
  /// to a one-point <see cref="Pointcloud"/> — the points body does not distinguish
  /// the two.
  /// </remarks>
  public static Base Decode(ReadOnlySpan<byte> bytes)
  {
    var header = ReadHeader(bytes);
    if ((header.Flags & SgeoFlags.Quantized) != 0)
    {
      throw new SpeckleException("Quantized SGEO layout is not supported (reserved for a future iteration).");
    }
    string units = Units.GetUnitFromEncoding(header.UnitsCode);
    var r = new Reader(bytes, SgeoFormat.HeaderSize);

    switch (header.PrimitiveType)
    {
      case SgeoPrimitiveType.Mesh:
      {
        int vCount = (int)r.U();
        int fCount = (int)r.U();
        var verts = new List<double>(vCount * 3);
        for (int i = 0; i < vCount * 3; i++) { verts.Add(r.D()); }
        var faces = new List<int>(fCount);
        for (int i = 0; i < fCount; i++) { faces.Add(r.I()); }
        var normals = new List<double>();
        if ((header.Flags & SgeoFlags.HasNormals) != 0)
        {
          r.Align8();
          for (int i = 0; i < vCount * 3; i++) { normals.Add(r.D()); }
        }
        var uvs = new List<double>();
        if ((header.Flags & SgeoFlags.HasUvs) != 0)
        {
          r.Align8();
          for (int i = 0; i < vCount * 2; i++) { uvs.Add(r.D()); }
        }
        var colors = new List<int>();
        if ((header.Flags & SgeoFlags.HasColors) != 0)
        {
          for (int i = 0; i < vCount; i++) { colors.Add(r.I()); }
        }
        return new Mesh
        {
          vertices = verts,
          faces = faces,
          vertexNormals = normals,
          textureCoordinates = uvs,
          colors = colors,
          units = units,
        };
      }

      case SgeoPrimitiveType.Line:
      {
        double ds = r.D();
        double de = r.D();
        var start = ReadPoint(ref r, units);
        var end = ReadPoint(ref r, units);
        return new Line
        {
          start = start,
          end = end,
          units = units,
          domain = new Interval { start = ds, end = de },
        };
      }

      case SgeoPrimitiveType.Polyline:
      {
        int count = (int)r.U();
        _ = r.U(); // reserved
        var value = new List<double>(count * 3);
        for (int i = 0; i < count * 3; i++) { value.Add(r.D()); }
        return new Polyline
        {
          value = value,
          closed = (header.Flags & SgeoFlags.Closed) != 0,
          units = units,
        };
      }

      case SgeoPrimitiveType.Polycurve:
      {
        int segCount = (int)r.U();
        _ = r.U(); // reserved
        var segments = new List<ICurve>(segCount);
        for (int i = 0; i < segCount; i++)
        {
          int blobLen = (int)r.U();
          _ = r.U(); // reserved
          segments.Add((ICurve)Decode(r.Slice(blobLen)));
          r.Align8();
        }
        return new Polycurve
        {
          segments = segments,
          closed = (header.Flags & SgeoFlags.Closed) != 0,
          units = units,
        };
      }

      case SgeoPrimitiveType.Curve:
      {
        int degree = (int)r.U();
        int cpCount = (int)r.U();
        int knotCount = (int)r.U();
        _ = r.U(); // reserved
        double ds = r.D();
        double de = r.D();
        bool rational = (header.Flags & SgeoFlags.Rational) != 0;
        var points = new List<double>(cpCount * 3);
        for (int i = 0; i < cpCount * 3; i++) { points.Add(r.D()); }
        var weights = new List<double>(cpCount);
        if (rational)
        {
          for (int i = 0; i < cpCount; i++) { weights.Add(r.D()); }
        }
        else
        {
          for (int i = 0; i < cpCount; i++) { weights.Add(1.0); }
        }
        var knots = new List<double>(knotCount);
        for (int i = 0; i < knotCount; i++) { knots.Add(r.D()); }
        return new Curve
        {
          degree = degree,
          periodic = (header.Flags & SgeoFlags.Periodic) != 0,
          rational = rational,
          closed = (header.Flags & SgeoFlags.Closed) != 0,
          points = points,
          weights = weights,
          knots = knots,
          domain = new Interval { start = ds, end = de },
          displayValue = new Polyline { value = new(), units = units },
          units = units,
        };
      }

      case SgeoPrimitiveType.Arc:
      {
        var plane = ReadPlane(ref r, units);
        var startPoint = ReadPoint(ref r, units);
        var midPoint = ReadPoint(ref r, units);
        var endPoint = ReadPoint(ref r, units);
        double ds = r.D();
        double de = r.D();
        return new Arc
        {
          plane = plane,
          startPoint = startPoint,
          midPoint = midPoint,
          endPoint = endPoint,
          units = units,
          domain = new Interval { start = ds, end = de },
        };
      }

      case SgeoPrimitiveType.Circle:
      {
        double radius = r.D();
        double ds = r.D();
        double de = r.D();
        var plane = ReadPlane(ref r, units);
        return new Circle
        {
          radius = radius,
          plane = plane,
          units = units,
          domain = new Interval { start = ds, end = de },
        };
      }

      case SgeoPrimitiveType.Points:
      {
        int count = (int)r.U();
        _ = r.U(); // reserved
        var points = new List<double>(count * 3);
        for (int i = 0; i < count * 3; i++) { points.Add(r.D()); }
        var colors = new List<int>();
        if ((header.Flags & SgeoFlags.HasColors) != 0)
        {
          for (int i = 0; i < count; i++) { colors.Add(r.I()); }
        }
        var sizes = new List<double>();
        if ((header.Flags & SgeoFlags.HasSizes) != 0)
        {
          r.Align8();
          for (int i = 0; i < count; i++) { sizes.Add(r.D()); }
        }
        return new Pointcloud
        {
          points = points,
          colors = colors,
          sizes = sizes,
          units = units,
        };
      }

      case SgeoPrimitiveType.Ellipse:
      {
        double firstRadius = r.D();
        double secondRadius = r.D();
        double ds = r.D();
        double de = r.D();
        var plane = ReadPlane(ref r, units);
        Interval? trim = null;
        if ((header.Flags & SgeoFlags.HasTrimDomain) != 0)
        {
          trim = new Interval { start = r.D(), end = r.D() };
        }
        return new Ellipse
        {
          firstRadius = firstRadius,
          secondRadius = secondRadius,
          plane = plane,
          domain = new Interval { start = ds, end = de },
          trimDomain = trim,
          units = units,
        };
      }

      case SgeoPrimitiveType.Spiral:
      {
        var spiralType = (SpiralType)r.U();
        _ = r.U(); // reserved
        var startPoint = ReadPoint(ref r, units);
        var endPoint = ReadPoint(ref r, units);
        var plane = ReadPlane(ref r, units);
        double turns = r.D();
        var pitchAxis = ReadVector(ref r, units);
        double pitch = r.D();
        double ds = r.D();
        double de = r.D();
        return new Spiral
        {
          startPoint = startPoint,
          endPoint = endPoint,
          plane = plane,
          turns = turns,
          pitchAxis = pitchAxis,
          pitch = pitch,
          spiralType = spiralType,
          units = units,
          length = 0, // derived; not stored
          domain = new Interval { start = ds, end = de },
          displayValue = new Polyline { value = new(), units = units },
        };
      }

      case SgeoPrimitiveType.Box:
      {
        var plane = ReadPlane(ref r, units);
        var xSize = new Interval { start = r.D(), end = r.D() };
        var ySize = new Interval { start = r.D(), end = r.D() };
        var zSize = new Interval { start = r.D(), end = r.D() };
        return new Box
        {
          plane = plane,
          xSize = xSize,
          ySize = ySize,
          zSize = zSize,
          units = units,
        };
      }

      default:
        throw new SpeckleException($"Unknown SGEO primitive type {(byte)header.PrimitiveType}.");
    }
  }

  private static Point ReadPoint(ref Reader r, string units)
  {
    double x = r.D();
    double y = r.D();
    double z = r.D();
    return new Point(x, y, z, units);
  }

  private static Vector ReadVector(ref Reader r, string units)
  {
    double x = r.D();
    double y = r.D();
    double z = r.D();
    return new Vector(x, y, z, units);
  }

  private static Plane ReadPlane(ref Reader r, string units) =>
    new()
    {
      origin = ReadPoint(ref r, units),
      normal = ReadVector(ref r, units),
      xdir = ReadVector(ref r, units),
      ydir = ReadVector(ref r, units),
      units = units,
    };

  /// <summary>Sequential little-endian reader over an SGEO body span.</summary>
  private ref struct Reader
  {
    private readonly ReadOnlySpan<byte> _bytes;
    private int _offset;

    public Reader(ReadOnlySpan<byte> bytes, int offset)
    {
      _bytes = bytes;
      _offset = offset;
    }

    public double D()
    {
      double v = MeshBinaryEncoder.ReadDoubleLE(_bytes.Slice(_offset, 8));
      _offset += 8;
      return v;
    }

    public int I()
    {
      int v = BinaryPrimitives.ReadInt32LittleEndian(_bytes.Slice(_offset, 4));
      _offset += 4;
      return v;
    }

    public uint U()
    {
      uint v = BinaryPrimitives.ReadUInt32LittleEndian(_bytes.Slice(_offset, 4));
      _offset += 4;
      return v;
    }

    public ReadOnlySpan<byte> Slice(int length)
    {
      var s = _bytes.Slice(_offset, length);
      _offset += length;
      return s;
    }

    public void Align8() => _offset = (_offset + 7) & ~7;
  }
}
