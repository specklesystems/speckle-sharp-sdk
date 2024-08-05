using Speckle.Core.Models;
using Speckle.Objects.Geometry;

namespace Speckle.Objects.BuiltElements.AdvanceSteel;

public class AsteelGrating : Base, IAsteelObject
{
  [DetachProperty]
  public List<Mesh> displayValue { get; set; }

  public Base userAttributes { get; set; }

  public Base asteelProperties { get; set; }

  //[SchemaInfo("AsteelGrating", "Creates a Advance Steel grating.", "Advance Steel", "Structure")]
  public AsteelGrating() { }
}
