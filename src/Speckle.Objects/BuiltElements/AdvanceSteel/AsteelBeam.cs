using Speckle.Objects.Structural.Materials;
using Speckle.Objects.Structural.Properties.Profiles;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements.AdvanceSteel;

[SpeckleType("Objects.BuiltElements.AdvanceSteel.AsteelBeam")]
public class AsteelBeam : Beam, IHasVolume, IHasArea, IAsteelObject
{
  [DetachProperty]
  public SectionProfile profile { get; set; }

  [DetachProperty]
  public StructuralMaterial material { get; set; }

  [DetachProperty]
  public AsteelSectionProfile asteelProfile { get; set; }

  public double volume { get; set; }
  public double area { get; set; }
  public Base userAttributes { get; set; }

  public Base asteelProperties { get; set; }

  public AsteelBeam() { }

  [SchemaInfo("AsteelBeam", "Creates a Advance Steel beam by curve.", "Advance Steel", "Structure")]
  public AsteelBeam([SchemaMainParam] ICurve baseLine, SectionProfile profile, StructuralMaterial material)
  {
    this.baseLine = baseLine;
    this.profile = profile;
    this.material = material;
  }
}
