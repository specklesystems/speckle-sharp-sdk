using Speckle.Objects.Primitive;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Geometry;

/// <summary>
/// Represents an edge of the <see cref="Brep"/>.
/// </summary>
[SpeckleType("Objects.Geometry.BrepEdge")]
public class BrepEdge : Base
{
  [Newtonsoft.Json.JsonIgnore]
  [System.Text.Json.Serialization.JsonIgnore]
  public required Brep Brep { get; set; }

  public required int Curve3dIndex { get; set; }
  public required int[] TrimIndices { get; set; }
  public required int StartIndex { get; set; }
  public required int EndIndex { get; set; }

  public required bool ProxyCurveIsReversed { get; set; }

  public required Interval Domain { get; set; }

  [Newtonsoft.Json.JsonIgnore]
  [System.Text.Json.Serialization.JsonIgnore]
  public Point StartVertex => Brep.Vertices[StartIndex];

  [Newtonsoft.Json.JsonIgnore]
  [System.Text.Json.Serialization.JsonIgnore]
  public Point EndVertex => Brep.Vertices[EndIndex];

  [Newtonsoft.Json.JsonIgnore]
  [System.Text.Json.Serialization.JsonIgnore]
  public IEnumerable<BrepTrim> Trims => TrimIndices.Select(i => Brep.Trims[i]);

  [Newtonsoft.Json.JsonIgnore]
  [System.Text.Json.Serialization.JsonIgnore]
  public ICurve Curve => Brep.Curve3D[Curve3dIndex];
}
