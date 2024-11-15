using Speckle.Sdk.Models;

namespace Speckle.Objects.Geometry;

/// <summary>
/// Represents a UV Trim Closed Loop on one of the <see cref="Brep"/>'s surfaces.
/// </summary>
[SpeckleType("Objects.Geometry.BrepLoop")]
public class BrepLoop : Base
{
  [Newtonsoft.Json.JsonIgnore]
  [System.Text.Json.Serialization.JsonIgnore]
  public required Brep Brep { get; set; }

  public required int FaceIndex { get; set; }
  public required List<int> TrimIndices { get; set; }
  public required BrepLoopType Type { get; set; }

  [Newtonsoft.Json.JsonIgnore]
  [System.Text.Json.Serialization.JsonIgnore]
  public BrepFace Face => Brep.Faces[FaceIndex];

  [Newtonsoft.Json.JsonIgnore]
  [System.Text.Json.Serialization.JsonIgnore]
  public List<BrepTrim> Trims => TrimIndices.Select(i => Brep.Trims[i]).ToList();
}
