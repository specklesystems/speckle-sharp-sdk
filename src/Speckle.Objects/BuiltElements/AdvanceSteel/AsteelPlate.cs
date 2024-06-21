using Speckle.Core.Kits;
using Speckle.Core.Models;
using Speckle.Objects.Geometry;
using Speckle.Objects.Structural.Materials;

namespace Speckle.Objects.BuiltElements.AdvanceSteel;

public class AsteelPlate : Area, IDisplayValue<List<Mesh>>, IHasArea, IHasVolume, IAsteelObject
{
  [DetachProperty]
  public StructuralMaterial? material { get; set; }

  public Base userAttributes { get; set; }

  public Base asteelProperties { get; set; }

  [SchemaInfo("AsteelPlate", "Creates a Advance Steel plate.", "Advance Steel", "Structure")]
  public AsteelPlate(Polyline outline, string units, StructuralMaterial? material = null)
  {
    this.outline = outline;
    this.material = material;
    this.units = units;
  }

  public AsteelPlate() { }
}
