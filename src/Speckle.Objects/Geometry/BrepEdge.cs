using System.Diagnostics.CodeAnalysis;
using Speckle.Newtonsoft.Json;
using Speckle.Objects.Primitive;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Geometry;

/// <summary>
/// Represents an edge of the <see cref="Brep"/>.
/// </summary>
[SpeckleType("Objects.Geometry.BrepEdge")]
[SuppressMessage("Performance", "CA1819:Properties should not return arrays")]
[Obsolete($"Breps have been replaced with {nameof(BrepX)}")]
public class BrepEdge : Base
{
  [JsonIgnore]
  public required Brep Brep { get; set; }

  public required int Curve3dIndex { get; set; }
  public required int[] TrimIndices { get; set; }
  public required int StartIndex { get; set; }
  public required int EndIndex { get; set; }

  public required bool ProxyCurveIsReversed { get; set; }

  public required Interval Domain { get; set; }

  [JsonIgnore]
  public Point StartVertex => Brep.Vertices[StartIndex];

  [JsonIgnore]
  public Point EndVertex => Brep.Vertices[EndIndex];

  [JsonIgnore]
  public IEnumerable<BrepTrim> Trims => TrimIndices.Select(i => Brep.Trims[i]);

  [JsonIgnore]
  public ICurve Curve => Brep.Curve3D[Curve3dIndex];
}
