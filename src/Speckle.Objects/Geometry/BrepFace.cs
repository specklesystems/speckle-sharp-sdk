using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Geometry;

/// <summary>
/// Represents a face on a <see cref="Brep"/>
/// </summary>
[SpeckleType("Objects.Geometry.BrepFace")]
public class BrepFace : Base
{
  public BrepFace() { }

  public BrepFace(Brep brep, int surfaceIndex, List<int> loopIndices, int outerLoopIndex, bool orientationReversed)
  {
    Brep = brep;
    SurfaceIndex = surfaceIndex;
    LoopIndices = loopIndices;
    OuterLoopIndex = outerLoopIndex;
    OrientationReversed = orientationReversed;
  }

  [JsonIgnore]
  public Brep Brep { get; set; }

  public int SurfaceIndex { get; set; }
  public List<int> LoopIndices { get; set; }
  public int OuterLoopIndex { get; set; }
  public bool OrientationReversed { get; set; }

  [JsonIgnore]
  public BrepLoop OuterLoop => Brep.Loops[OuterLoopIndex];

  [JsonIgnore]
  public Surface Surface => Brep.Surfaces[SurfaceIndex];

  [JsonIgnore]
  public List<BrepLoop> Loops => LoopIndices.Select(i => Brep.Loops[i]).ToList();
}
