using Speckle.Objects.Geometry;
using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements.AdvanceSteel;

public class AsteelSpecialPart : Base, IAsteelObject
{
  [DetachProperty]
  public List<Mesh> displayValue { get; set; }

  public Base userAttributes { get; set; }

  public Base asteelProperties { get; set; }

  //[SchemaInfo("AsteelSpecialPart", "Creates a Advance Steel special part.", "Advance Steel", "Structure")]
  public AsteelSpecialPart() { }
}
