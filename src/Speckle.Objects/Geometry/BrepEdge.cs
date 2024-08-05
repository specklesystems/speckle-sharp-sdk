using Speckle.Newtonsoft.Json;
using Speckle.Objects.Primitive;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Geometry;

/// <summary>
/// Represents an edge of the <see cref="Brep"/>.
/// </summary>
public class BrepEdge : Base
{
  public BrepEdge() { }

  public BrepEdge(
    Brep brep,
    int curve3dIndex,
    int[] trimIndices,
    int startIndex,
    int endIndex,
    bool proxyCurvedIsReversed,
    Interval? domain
  )
  {
    Brep = brep;
    Curve3dIndex = curve3dIndex;
    TrimIndices = trimIndices;
    StartIndex = startIndex;
    EndIndex = endIndex;
    ProxyCurveIsReversed = proxyCurvedIsReversed;
    Domain = domain ?? new(0, 1);
  }

  [JsonIgnore]
  public Brep Brep { get; set; }

  public int Curve3dIndex { get; set; }
  public int[] TrimIndices { get; set; }
  public int StartIndex { get; set; }
  public int EndIndex { get; set; }

  public bool ProxyCurveIsReversed { get; set; }

  public Interval Domain { get; set; } = new(0, 1);

  [JsonIgnore]
  public Point StartVertex => Brep.Vertices[StartIndex];

  [JsonIgnore]
  public Point EndVertex => Brep.Vertices[EndIndex];

  [JsonIgnore]
  public IEnumerable<BrepTrim> Trims => TrimIndices.Select(i => Brep.Trims[i]);

  [JsonIgnore]
  public ICurve Curve => Brep.Curve3D[Curve3dIndex];
}
