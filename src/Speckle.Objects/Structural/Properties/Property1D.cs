using Speckle.Objects.Structural.Geometry;
using Speckle.Objects.Structural.Materials;
using Speckle.Objects.Structural.Properties.Profiles;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Structural.Properties;

[SpeckleType("Objects.Structural.Properties.Property1D")]
public class Property1D : Property //SectionProperty as alt class name
{
  public Property1D() { }

  [SchemaInfo("Property1D (by name)", "Creates a Speckle structural 1D element property", "Structural", "Properties")]
  public Property1D(string name)
  {
    this.name = name;
  }

  [SchemaInfo("Property1D", "Creates a Speckle structural 1D element property", "Structural", "Properties")]
  public Property1D(string name, StructuralMaterial? material, SectionProfile profile)
  {
    this.name = name;
    this.material = material;
    this.profile = profile;
  }

  public MemberType memberType { get; set; }

  [DetachProperty]
  public StructuralMaterial? material { get; set; }

  [DetachProperty]
  public SectionProfile profile { get; set; } //section description

  public BaseReferencePoint referencePoint { get; set; }
  public double offsetY { get; set; } //offset from reference point
  public double offsetZ { get; set; } //offset from reference point
}
