using Speckle.Newtonsoft.Json;
using Speckle.Objects.Primitive;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Geometry;

/// <summary>
/// Represents a UV Trim curve for one of the <see cref="Brep"/>'s surfaces.
/// </summary>
[SpeckleType("Objects.Geometry.BrepTrim")]
[Obsolete($"Breps have been replaced with {nameof(BrepX)}")]
public class BrepTrim : Base
{
  [JsonIgnore]
  public required Brep Brep { get; set; }
  public required int EdgeIndex { get; set; }
  public required int StartIndex { get; set; }
  public required int EndIndex { get; set; }
  public required int FaceIndex { get; set; }
  public required int LoopIndex { get; set; }
  public required int CurveIndex { get; set; }
  public required int IsoStatus { get; set; }
  public required BrepTrimType TrimType { get; set; }
  public required bool IsReversed { get; set; }

  public required Interval Domain { get; set; }

  [JsonIgnore]
  public BrepFace Face => Brep.Faces[FaceIndex];

  [JsonIgnore]
  public BrepLoop Loop => Brep.Loops[LoopIndex];

  [JsonIgnore]
  public BrepEdge? Edge => EdgeIndex != -1 ? Brep.Edges[EdgeIndex] : null;

  [JsonIgnore]
  public ICurve Curve2d => Brep.Curve2D[CurveIndex];
}
