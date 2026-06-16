using System.Buffers.Binary;
using Speckle.Objects.Geometry;
using Speckle.Sdk;
using Speckle.Sdk.Common;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Utils;

/// <summary>
/// Encodes Speckle geometry primitives into SGEO v1 byte buffers (see
/// <see cref="SgeoFormat"/>). One blob per geometry buffer; the SDK owns the
/// format. Stores only the authoritative definition — derived fields
/// (length/area/volume/bbox, arc radius/measure) are recomputed on decode.
/// </summary>
public static class SgeoEncoder
{
  /// <summary>
  /// Encodes a supported geometry object into an SGEO blob.
  /// </summary>
  /// <exception cref="SpeckleException">when the geometry type has no SGEO mapping.</exception>
  public static byte[] Encode(Base geometry)
  {
    _ = geometry.NotNull();
    return geometry switch
    {
      Mesh m => EncodeMesh(m),
      Line l => EncodeLine(l),
      Polyline p => EncodePolyline(p),
      Polycurve pc => EncodePolycurve(pc),
      Curve c => EncodeCurve(c),
      Arc a => EncodeArc(a),
      Circle ci => EncodeCircle(ci),
      Point pt => EncodePoint(pt),
      Pointcloud pcl => EncodePointcloud(pcl),
      Ellipse e => EncodeEllipse(e),
      Spiral s => EncodeSpiral(s),
      Box b => EncodeBox(b),
      _ => throw new SpeckleException($"No SGEO encoding for geometry type '{geometry.GetType().Name}'."),
    };
  }

  /// <summary>Returns true and the SGEO primitive type if the object is encodable.</summary>
  public static bool TryGetPrimitiveType(Base geometry, out SgeoPrimitiveType type)
  {
    type = geometry switch
    {
      Mesh => SgeoPrimitiveType.Mesh,
      Line => SgeoPrimitiveType.Line,
      Polyline => SgeoPrimitiveType.Polyline,
      Polycurve => SgeoPrimitiveType.Polycurve,
      Curve => SgeoPrimitiveType.Curve,
      Arc => SgeoPrimitiveType.Arc,
      Circle => SgeoPrimitiveType.Circle,
      Point or Pointcloud => SgeoPrimitiveType.Points,
      Ellipse => SgeoPrimitiveType.Ellipse,
      Spiral => SgeoPrimitiveType.Spiral,
      Box => SgeoPrimitiveType.Box,
      _ => (SgeoPrimitiveType)255,
    };
    return (byte)type != 255;
  }

  // ── primitives ─────────────────────────────────────────────────────────

  private static byte[] EncodeMesh(Mesh m)
  {
    if (m.vertices.Count % 3 != 0)
    {
      throw new SpeckleException("Mesh.vertices length must be a multiple of 3.");
    }
    bool hasNormals = m.vertexNormals.Count > 0;
    bool hasUvs = m.textureCoordinates.Count > 0;
    bool hasColors = m.colors.Count > 0;

    var flags = SgeoFlags.None;
    if (hasNormals) { flags |= SgeoFlags.HasNormals; }
    if (hasUvs) { flags |= SgeoFlags.HasUvs; }
    if (hasColors) { flags |= SgeoFlags.HasColors; }

    var body = new List<byte>(m.vertices.Count * 8 + m.faces.Count * 4 + 16);
    AddUInt32(body, (uint)(m.vertices.Count / 3));
    AddUInt32(body, (uint)m.faces.Count);
    foreach (var v in m.vertices) { AddDouble(body, v); }
    foreach (var f in m.faces) { AddInt32(body, f); }
    if (hasNormals) { Pad8(body); foreach (var n in m.vertexNormals) { AddDouble(body, n); } }
    if (hasUvs) { Pad8(body); foreach (var t in m.textureCoordinates) { AddDouble(body, t); } }
    if (hasColors) { foreach (var c in m.colors) { AddInt32(body, c); } }

    return Assemble(SgeoPrimitiveType.Mesh, flags, m.units, body);
  }

  private static byte[] EncodeLine(Line l)
  {
    var body = new List<byte>(64);
    AddDouble(body, l.domain?.start ?? 0);
    AddDouble(body, l.domain?.end ?? 1);
    AddPoint(body, l.start);
    AddPoint(body, l.end);
    return Assemble(SgeoPrimitiveType.Line, SgeoFlags.None, l.units, body);
  }

  private static byte[] EncodePolyline(Polyline p)
  {
    if (p.value.Count % 3 != 0)
    {
      throw new SpeckleException("Polyline.value length must be a multiple of 3.");
    }
    var flags = p.closed ? SgeoFlags.Closed : SgeoFlags.None;
    var body = new List<byte>(p.value.Count * 8 + 8);
    AddUInt32(body, (uint)(p.value.Count / 3));
    AddUInt32(body, 0);
    foreach (var v in p.value) { AddDouble(body, v); }
    return Assemble(SgeoPrimitiveType.Polyline, flags, p.units, body);
  }

  private static byte[] EncodePolycurve(Polycurve pc)
  {
    var flags = pc.closed ? SgeoFlags.Closed : SgeoFlags.None;
    var body = new List<byte>(64);
    AddUInt32(body, (uint)pc.segments.Count);
    AddUInt32(body, 0);
    foreach (var seg in pc.segments)
    {
      byte[] blob = Encode((Base)seg);
      AddUInt32(body, (uint)blob.Length);
      AddUInt32(body, 0);
      body.AddRange(blob);
      Pad8(body);
    }
    return Assemble(SgeoPrimitiveType.Polycurve, flags, pc.units, body);
  }

  private static byte[] EncodeCurve(Curve c)
  {
    if (c.points.Count % 3 != 0)
    {
      throw new SpeckleException("Curve.points length must be a multiple of 3.");
    }
    var flags = SgeoFlags.None;
    if (c.rational) { flags |= SgeoFlags.Rational; }
    if (c.periodic) { flags |= SgeoFlags.Periodic; }
    if (c.closed) { flags |= SgeoFlags.Closed; }

    var body = new List<byte>(c.points.Count * 8 + c.knots.Count * 8 + 32);
    AddUInt32(body, (uint)c.degree);
    AddUInt32(body, (uint)(c.points.Count / 3));
    AddUInt32(body, (uint)c.knots.Count);
    AddUInt32(body, 0);
    AddDouble(body, c.domain?.start ?? 0);
    AddDouble(body, c.domain?.end ?? 1);
    foreach (var p in c.points) { AddDouble(body, p); }
    if (c.rational) { foreach (var w in c.weights) { AddDouble(body, w); } }
    foreach (var k in c.knots) { AddDouble(body, k); }
    return Assemble(SgeoPrimitiveType.Curve, flags, c.units, body);
  }

  private static byte[] EncodeArc(Arc a)
  {
    var body = new List<byte>(184);
    AddPlane(body, a.plane);
    AddPoint(body, a.startPoint);
    AddPoint(body, a.midPoint);
    AddPoint(body, a.endPoint);
    AddDouble(body, a.domain?.start ?? 0);
    AddDouble(body, a.domain?.end ?? 0);
    return Assemble(SgeoPrimitiveType.Arc, SgeoFlags.None, a.units, body);
  }

  private static byte[] EncodeCircle(Circle c)
  {
    var body = new List<byte>(120);
    AddDouble(body, c.radius);
    AddDouble(body, c.domain?.start ?? 0);
    AddDouble(body, c.domain?.end ?? 1);
    AddPlane(body, c.plane);
    return Assemble(SgeoPrimitiveType.Circle, SgeoFlags.None, c.units, body);
  }

  private static byte[] EncodePoint(Point pt)
  {
    var body = new List<byte>(32);
    AddUInt32(body, 1);
    AddUInt32(body, 0);
    AddPoint(body, pt);
    return Assemble(SgeoPrimitiveType.Points, SgeoFlags.None, pt.units, body);
  }

  private static byte[] EncodePointcloud(Pointcloud p)
  {
    if (p.points.Count % 3 != 0)
    {
      throw new SpeckleException("Pointcloud.points length must be a multiple of 3.");
    }
    bool hasColors = p.colors.Count > 0;
    bool hasSizes = p.sizes.Count > 0;
    var flags = SgeoFlags.None;
    if (hasColors) { flags |= SgeoFlags.HasColors; }
    if (hasSizes) { flags |= SgeoFlags.HasSizes; }

    var body = new List<byte>(p.points.Count * 8 + 8);
    AddUInt32(body, (uint)(p.points.Count / 3));
    AddUInt32(body, 0);
    foreach (var v in p.points) { AddDouble(body, v); }
    if (hasColors) { foreach (var c in p.colors) { AddInt32(body, c); } }
    if (hasSizes) { Pad8(body); foreach (var s in p.sizes) { AddDouble(body, s); } }
    return Assemble(SgeoPrimitiveType.Points, flags, p.units, body);
  }

  private static byte[] EncodeEllipse(Ellipse e)
  {
    var flags = e.trimDomain != null ? SgeoFlags.HasTrimDomain : SgeoFlags.None;
    var body = new List<byte>(160);
    AddDouble(body, e.firstRadius);
    AddDouble(body, e.secondRadius);
    AddDouble(body, e.domain.start);
    AddDouble(body, e.domain.end);
    AddPlane(body, e.plane);
    if (e.trimDomain != null)
    {
      AddDouble(body, e.trimDomain.start);
      AddDouble(body, e.trimDomain.end);
    }
    return Assemble(SgeoPrimitiveType.Ellipse, flags, e.units, body);
  }

  private static byte[] EncodeSpiral(Spiral s)
  {
    var body = new List<byte>(224);
    AddUInt32(body, (uint)s.spiralType);
    AddUInt32(body, 0);
    AddPoint(body, s.startPoint);
    AddPoint(body, s.endPoint);
    AddPlane(body, s.plane);
    AddDouble(body, s.turns);
    AddVector(body, s.pitchAxis);
    AddDouble(body, s.pitch);
    AddDouble(body, s.domain.start);
    AddDouble(body, s.domain.end);
    return Assemble(SgeoPrimitiveType.Spiral, SgeoFlags.None, s.units, body);
  }

  private static byte[] EncodeBox(Box b)
  {
    var body = new List<byte>(160);
    AddPlane(body, b.plane);
    AddDouble(body, b.xSize.start);
    AddDouble(body, b.xSize.end);
    AddDouble(body, b.ySize.start);
    AddDouble(body, b.ySize.end);
    AddDouble(body, b.zSize.start);
    AddDouble(body, b.zSize.end);
    return Assemble(SgeoPrimitiveType.Box, SgeoFlags.None, b.units, body);
  }

  // ── assembly + low-level writers ───────────────────────────────────────

  private static byte[] Assemble(SgeoPrimitiveType type, SgeoFlags flags, string units, List<byte> body)
  {
    var buf = new byte[SgeoFormat.HeaderSize + body.Count];
    var span = buf.AsSpan();
    SgeoFormat.Magic.CopyTo(span); // 0x00..0x03
    span[4] = SgeoFormat.Version1; // 0x04
    span[5] = (byte)type; // 0x05
    BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(6, 2), (ushort)flags); // 0x06
    BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(8, 2), (ushort)Units.GetEncodingFromUnit(units)); // 0x08
    BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(10, 2), 0); // 0x0A reserved
    for (int i = 0; i < body.Count; i++)
    {
      buf[SgeoFormat.HeaderSize + i] = body[i];
    }
    uint crc = Crc32.Compute(span.Slice(SgeoFormat.HeaderSize));
    BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(12, 4), crc); // 0x0C
    return buf;
  }

  private static void AddPoint(List<byte> b, Point p)
  {
    AddDouble(b, p.x);
    AddDouble(b, p.y);
    AddDouble(b, p.z);
  }

  private static void AddVector(List<byte> b, Vector v)
  {
    AddDouble(b, v.x);
    AddDouble(b, v.y);
    AddDouble(b, v.z);
  }

  private static void AddPlane(List<byte> b, Plane p)
  {
    AddPoint(b, p.origin);
    AddVector(b, p.normal);
    AddVector(b, p.xdir);
    AddVector(b, p.ydir);
  }

  private static void AddDouble(List<byte> b, double v)
  {
    long bits = BitConverter.DoubleToInt64Bits(v);
    for (int i = 0; i < 8; i++)
    {
      b.Add((byte)(bits >> (8 * i)));
    }
  }

  private static void AddInt32(List<byte> b, int v)
  {
    for (int i = 0; i < 4; i++)
    {
      b.Add((byte)(v >> (8 * i)));
    }
  }

  private static void AddUInt32(List<byte> b, uint v)
  {
    for (int i = 0; i < 4; i++)
    {
      b.Add((byte)(v >> (8 * i)));
    }
  }

  private static void Pad8(List<byte> b)
  {
    while (b.Count % 8 != 0)
    {
      b.Add(0);
    }
  }
}
