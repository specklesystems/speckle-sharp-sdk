using Speckle.Core.Models;
using Speckle.Objects.Geometry;

namespace Speckle.Objects.BuiltElements.AdvanceSteel;

public abstract class AsteelBolt : Base, IAsteelObject
{
  [DetachProperty]
  public List<Mesh> displayValue { get; set; }

  public Base userAttributes { get; set; }

  public Base asteelProperties { get; set; }
}

public class AsteelCircularBolt : AsteelBolt
{
  //[SchemaInfo("AsteelCircularBolt", "Creates a Advance Steel circular bolt.", "Advance Steel", "Structure")]
  public AsteelCircularBolt() { }
}

public class AsteelRectangularBolt : AsteelBolt
{
  //[SchemaInfo("AsteelRectangularBolt", "Creates a Advance Steel rectangular bolt.", "Advance Steel", "Structure")]
  public AsteelRectangularBolt() { }
}
