using Speckle.Objects.Geometry;
using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements.AdvanceSteel;

public abstract class AsteelBolt : Base, IAsteelObject
{
  [DetachProperty]
  public List<Mesh> displayValue { get; set; }

  public Base userAttributes { get; set; }

  public Base asteelProperties { get; set; }
}

[SpeckleType("Objects.BuiltElements.AdvanceSteel.AsteelCircularBolt")]
public class AsteelCircularBolt : AsteelBolt
{
  //[SchemaInfo("AsteelCircularBolt", "Creates a Advance Steel circular bolt.", "Advance Steel", "Structure")]
  public AsteelCircularBolt() { }
}

[SpeckleType("Objects.BuiltElements.AdvanceSteel.AsteelRectangularBolt")]
public class AsteelRectangularBolt : AsteelBolt
{
  //[SchemaInfo("AsteelRectangularBolt", "Creates a Advance Steel rectangular bolt.", "Advance Steel", "Structure")]
  public AsteelRectangularBolt() { }
}
