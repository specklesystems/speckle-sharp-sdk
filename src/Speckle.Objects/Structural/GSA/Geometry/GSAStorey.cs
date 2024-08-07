using Speckle.Objects.Structural.Geometry;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Structural.GSA.Geometry;

[SpeckleType("Objects.Structural.GSA.Geometry.GSAStorey")]
public class GSAStorey : Storey
{
  public GSAStorey() { }

  [SchemaInfo(
    "GSAStorey",
    "Creates a Speckle structural storey (to describe floor levels/storeys in the structural model) for GSA",
    "GSA",
    "Geometry"
  )]
  public GSAStorey(int nativeId, string name, Axis axis, double elevation, double toleranceBelow, double toleranceAbove)
  {
    this.nativeId = nativeId;
    this.name = name;
    this.axis = axis;
    this.elevation = elevation;
    this.toleranceBelow = toleranceBelow;
    this.toleranceAbove = toleranceAbove;
  }

  public int nativeId { get; set; }

  [DetachProperty]
  public Axis axis { get; set; }

  public double toleranceBelow { get; set; }
  public double toleranceAbove { get; set; }
}
