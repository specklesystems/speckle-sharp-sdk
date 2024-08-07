using Speckle.Objects.Structural.Materials;
using Speckle.Objects.Structural.Properties;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Structural.GSA.Properties;

[SpeckleType("Objects.Structural.GSA.Properties.Property2D")]
public class GSAProperty2D : Property2D
{
  public GSAProperty2D() { }

  [SchemaInfo("GSAProperty2D", "Creates a Speckle structural 2D element property for GSA", "GSA", "Properties")]
  public GSAProperty2D(int nativeId, string name, StructuralMaterial material, double thickness)
  {
    this.nativeId = nativeId;
    this.name = name;
    this.material = material;
    this.thickness = thickness;
  }

  public int nativeId { get; set; }

  [DetachProperty]
  public StructuralMaterial designMaterial { get; set; }

  public double cost { get; set; }
  public double additionalMass { get; set; }
  public string concreteSlabProp { get; set; }
  public string colour { get; set; }
}
