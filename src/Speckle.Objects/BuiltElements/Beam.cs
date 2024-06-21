using Speckle.Core.Kits;
using Speckle.Core.Models;
using Speckle.Objects.Geometry;

namespace Speckle.Objects.BuiltElements;

public class Beam : Base, IDisplayValue<List<Mesh>>
{
  public Beam() { }

  [SchemaInfo("Beam", "Creates a Speckle beam", "BIM", "Structure")]
  public Beam([SchemaMainParam] ICurve baseLine)
  {
    this.baseLine = baseLine;
  }

  public ICurve baseLine { get; set; }

  public virtual Level? level { get; internal set; }

  public string units { get; set; }

  [DetachProperty]
  public List<Mesh> displayValue { get; set; }
}
