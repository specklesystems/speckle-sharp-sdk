using Speckle.Objects.Structural.Materials;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Structural.CSI.Materials;

[SpeckleType("Objects.Structural.CSI.Materials.CSIConcrete")]
public class CSIConcrete : Concrete
{
  public int SSHysType { get; set; }
  public int SSType { get; set; }

  public double finalSlope { get; set; }

  public double frictionAngle { get; set; }
  public double dialationalAngle { get; set; }
}
