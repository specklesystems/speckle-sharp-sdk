using Speckle.Objects.Geometry;
using Speckle.Objects.Structural.Materials;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements.AdvanceSteel;

public class AsteelSlab : Area, IDisplayValue<List<Mesh>>, IHasArea, IHasVolume, IAsteelObject
{
  [DetachProperty]
  public StructuralMaterial? material { get; set; }

  public Base userAttributes { get; set; }

  public Base asteelProperties { get; set; }

  [SchemaInfo("AsteelSlab", "Creates a Advance Steel slab.", "Advance Steel", "Structure")]
  public AsteelSlab(Polyline outline, string units, StructuralMaterial? material = null)
  {
    this.outline = outline;
    this.material = material;
    this.units = units;
  }

  public AsteelSlab() { }
}
