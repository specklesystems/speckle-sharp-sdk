using Speckle.Objects.Structural.GSA.Geometry;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Structural.GSA.Bridge;

[SpeckleType("Objects.Structural.GSA.Bridge.GSAAlignment")]
public class GSAAlignment : Base
{
  public GSAAlignment() { }

  [SchemaInfo(
    "GSAAlignment",
    "Creates a Speckle structural alignment for GSA (as a setting out feature for bridge models)",
    "GSA",
    "Bridge"
  )]
  public GSAAlignment(
    int nativeId,
    string name,
    GSAGridSurface gridSurface,
    List<double> chainage,
    List<double> curvature
  )
  {
    this.nativeId = nativeId;
    this.name = name;
    this.gridSurface = gridSurface;
    this.chainage = chainage;
    this.curvature = curvature;
  }

  public int nativeId { get; set; }
  public string name { get; set; }

  [DetachProperty]
  public GSAGridSurface gridSurface { get; set; }

  public List<double> chainage { get; set; }
  public List<double> curvature { get; set; }

  public int GetNumAlignmentPoints()
  {
    return chainage.Count + curvature.Count;
  }
}
