using Speckle.Sdk.Models;

namespace Speckle.Objects.Geometry;

/// <summary>
/// Represents a face on a <see cref="Brep"/>
/// </summary>
[SpeckleType("Objects.Geometry.BrepFace")]
public class BrepFace : Base
{
  [Newtonsoft.Json.JsonIgnore]
  [System.Text.Json.Serialization.JsonIgnore]
  public required Brep Brep { get; set; }

  public required int SurfaceIndex { get; set; }
  public required List<int> LoopIndices { get; set; }
  public required int OuterLoopIndex { get; set; }
  public required bool OrientationReversed { get; set; }

  [Newtonsoft.Json.JsonIgnore]
  [System.Text.Json.Serialization.JsonIgnore]
  public BrepLoop OuterLoop => Brep.Loops[OuterLoopIndex];

  [Newtonsoft.Json.JsonIgnore]
  [System.Text.Json.Serialization.JsonIgnore]
  public Surface Surface => Brep.Surfaces[SurfaceIndex];

  [Newtonsoft.Json.JsonIgnore]
  [System.Text.Json.Serialization.JsonIgnore]
  public List<BrepLoop> Loops => LoopIndices.Select(i => Brep.Loops[i]).ToList();
}
