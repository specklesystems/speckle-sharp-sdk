using Speckle.Objects.Geometry;
using Speckle.Objects.Utils;

namespace Speckle.Sdk.Artifacts.Harness;

/// <summary>
/// Deterministic byte-layout check for the Curve(4) + Spiral(9) SGEO encoding:
/// a leading <c>displayValue</c> polyline (render) followed by the FULL analytic
/// definition (kept for the analytical engine). Verifies the leading polyline is
/// recoverable at the fixed offset and the trailing def round-trips at
/// <c>def_off = 0x18 + display_point_count*3*8</c>.
/// </summary>
internal static class SgeoSelfTest
{
  private const string U = "m";

  public static int Run()
  {
    var ok = true;
    ok &= CheckCurve();
    ok &= CheckSpiral();
    Console.WriteLine(ok ? "\nSELFTEST PASS" : "\nSELFTEST FAIL");
    return ok ? 0 : 1;
  }

  private static bool CheckCurve()
  {
    // A degree-3 rational NURBS with a distinct displayValue (4 pts) so we can tell
    // the leading render polyline from the trailing control points.
    var display = new Polyline
    {
      value = new() { 0, 0, 0, 1, 1, 0, 2, 0, 0, 3, 1, 0 },
      closed = true,
      units = U,
    };
    var curve = new Curve
    {
      degree = 3,
      periodic = false,
      rational = true,
      points = new() { 10, 0, 0, 11, 5, 0, 12, 5, 0, 13, 0, 0 }, // 4 control pts
      weights = new() { 1, 0.5, 0.5, 1 },
      knots = new() { 0, 0, 0, 0, 1, 1, 1, 1 },
      closed = true,
      units = U,
      domain = new() { start = 0, end = 1 },
      displayValue = display,
    };

    var blob = SgeoEncoder.Encode(curve);
    var dv = View(blob);
    var fail = new List<string>();

    Expect(fail, "prim", blob[5], (byte)SgeoPrimitiveType.Curve);
    Expect(fail, "flag.closed", blob[6] & 0x02, 0x02);
    Expect(fail, "flag.rational", blob[6] & 0x04, 0x04);

    // leading displayValue polyline
    var dispCount = (int)dv.GetUInt32(0x10);
    Expect(fail, "display_count", dispCount, display.value.Count / 3);
    for (var i = 0; i < display.value.Count; i++)
    {
      Expect(fail, $"display[{i}]", dv.GetF64(0x18 + i * 8), display.value[i]);
    }

    // trailing NURBS def
    var defOff = 0x18 + dispCount * 3 * 8;
    Expect(fail, "degree", (int)dv.GetUInt32(defOff), 3);
    var cpCount = (int)dv.GetUInt32(defOff + 4);
    Expect(fail, "cp_count", cpCount, curve.points.Count / 3);
    Expect(fail, "knot_count", (int)dv.GetUInt32(defOff + 8), curve.knots.Count);
    Expect(fail, "domain.start", dv.GetF64(defOff + 0x10), 0);
    Expect(fail, "domain.end", dv.GetF64(defOff + 0x18), 1);
    var o = defOff + 0x20;
    for (var i = 0; i < curve.points.Count; i++)
    {
      Expect(fail, $"cp[{i}]", dv.GetF64(o + i * 8), curve.points[i]);
    }
    o += curve.points.Count * 8;
    for (var i = 0; i < curve.weights.Count; i++)
    {
      Expect(fail, $"w[{i}]", dv.GetF64(o + i * 8), curve.weights[i]);
    }
    o += curve.weights.Count * 8;
    for (var i = 0; i < curve.knots.Count; i++)
    {
      Expect(fail, $"knot[{i}]", dv.GetF64(o + i * 8), curve.knots[i]);
    }
    // header(16) + polyline[count+pad(8) + disp pts] + def header[degree+cp+knot+reserved(16) + domain(16)] + cps + weights + knots
    var expectedLen = 16 + 8 + dispCount * 3 * 8 + 32 + cpCount * 3 * 8 + curve.weights.Count * 8 + curve.knots.Count * 8;
    Expect(fail, "total_len", blob.Length, expectedLen);

    Report("Curve", blob.Length, fail);
    return fail.Count == 0;
  }

  private static bool CheckSpiral()
  {
    var display = new Polyline
    {
      value = new() { 0, 0, 0, 1, 0, 0.1, 2, 0, 0.2 },
      closed = false,
      units = U,
    };
    var spiral = new Spiral
    {
      startPoint = new Point(1, 2, 3, U),
      endPoint = new Point(4, 5, 6, U),
      plane = new Plane
      {
        origin = new Point(0, 0, 0, U),
        normal = new Vector(0, 0, 1, U),
        xdir = new Vector(1, 0, 0, U),
        ydir = new Vector(0, 1, 0, U),
        units = U,
      },
      turns = 2.5,
      pitchAxis = new Vector(0, 0, 1, U),
      pitch = 0.75,
      spiralType = SpiralType.Clothoid,
      units = U,
      length = 0,
      domain = new() { start = 0, end = 1 },
      displayValue = display,
    };

    var blob = SgeoEncoder.Encode(spiral);
    var dv = View(blob);
    var fail = new List<string>();

    Expect(fail, "prim", blob[5], (byte)SgeoPrimitiveType.Spiral);

    var dispCount = (int)dv.GetUInt32(0x10);
    Expect(fail, "display_count", dispCount, display.value.Count / 3);
    for (var i = 0; i < display.value.Count; i++)
    {
      Expect(fail, $"display[{i}]", dv.GetF64(0x18 + i * 8), display.value[i]);
    }

    var defOff = 0x18 + dispCount * 3 * 8;
    Expect(fail, "spiral_type", (int)dv.GetUInt32(defOff), (int)SpiralType.Clothoid);
    Expect(fail, "start.x", dv.GetF64(defOff + 0x08), 1);
    Expect(fail, "end.x", dv.GetF64(defOff + 0x20), 4);
    Expect(fail, "plane.origin.x", dv.GetF64(defOff + 0x38), 0);
    Expect(fail, "plane.normal.z", dv.GetF64(defOff + 0x50 + 16), 1);
    Expect(fail, "turns", dv.GetF64(defOff + 0x98), 2.5);
    Expect(fail, "pitchAxis.z", dv.GetF64(defOff + 0xA0 + 16), 1);
    Expect(fail, "pitch", dv.GetF64(defOff + 0xB8), 0.75);
    Expect(fail, "domain.start", dv.GetF64(defOff + 0xC0), 0);
    Expect(fail, "domain.end", dv.GetF64(defOff + 0xC8), 1);

    Report("Spiral", blob.Length, fail);
    return fail.Count == 0;
  }

  // — helpers —
  private readonly struct Reader(byte[] b)
  {
    public uint GetUInt32(int o) => BitConverter.ToUInt32(b, o);

    public double GetF64(int o) => BitConverter.ToDouble(b, o);
  }

  private static Reader View(byte[] b) => new(b);

  private static void Expect(List<string> fail, string label, double got, double want)
  {
    if (Math.Abs(got - want) > 1e-9)
    {
      fail.Add($"{label}: got {got} want {want}");
    }
  }

  private static void Report(string name, int len, List<string> fail)
  {
    if (fail.Count == 0)
    {
      Console.WriteLine($"  {name}: OK ({len}B)");
    }
    else
    {
      Console.WriteLine($"  {name}: {fail.Count} MISMATCH ({len}B)");
      foreach (var f in fail)
      {
        Console.WriteLine($"    ✗ {f}");
      }
    }
  }
}
