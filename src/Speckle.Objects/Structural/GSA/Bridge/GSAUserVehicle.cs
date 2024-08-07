using Speckle.Sdk.Host;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Structural.GSA.Bridge;

[SpeckleType("Objects.Structural.GSA.Bridge.GSAUserVehicle")]
public class GSAUserVehicle : Base
{
  public GSAUserVehicle() { }

  [SchemaInfo(
    "GSAUserVehicle",
    "Creates a Speckle structural user-defined vehicle (as a pattern of loading based on axle and wheel positions, for influence analysis) for GSA",
    "GSA",
    "Bridge"
  )]
  public GSAUserVehicle(
    int nativeId,
    string name,
    double width,
    List<double> axlePositions,
    List<double> axleOffsets,
    List<double> axleLeft,
    List<double> axleRight
  )
  {
    this.nativeId = nativeId;
    this.name = name;
    this.width = width;
    this.axlePositions = axlePositions;
    this.axleOffsets = axleOffsets;
    this.axleLeft = axleLeft;
    this.axleRight = axleRight;
  }

  public int nativeId { get; set; }
  public string name { get; set; }
  public double width { get; set; } //vehicle width
  public List<double> axlePositions { get; set; }
  public List<double> axleOffsets { get; set; } // offset from centreline
  public List<double> axleLeft { get; set; } //load on left side
  public List<double> axleRight { get; set; } //load on right side
}
