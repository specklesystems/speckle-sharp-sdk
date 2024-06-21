using Speckle.Core.Kits;
using Speckle.Core.Models;
using Speckle.Objects.Geometry;

namespace Speckle.Objects.BuiltElements;

public class Brace : Base, IDisplayValue<List<Mesh>>
{
  public Brace() { }

  [SchemaInfo("Brace", "Creates a Speckle brace", "BIM", "Structure")]
  public Brace([SchemaMainParam] ICurve baseLine)
  {
    this.baseLine = baseLine;
  }

  public ICurve baseLine { get; set; }

  public string units { get; set; }

  [DetachProperty]
  public List<Mesh> displayValue { get; set; }
}
