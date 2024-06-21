using Speckle.Core.Kits;
using Speckle.Core.Models;
using Speckle.Objects.Geometry;

namespace Speckle.Objects.BuiltElements;

public class Column : Base, IDisplayValue<List<Mesh>>
{
  public Column() { }

  [SchemaInfo("Column", "Creates a Speckle column", "BIM", "Structure")]
  public Column([SchemaMainParam] ICurve baseLine)
  {
    this.baseLine = baseLine;
  }

  public ICurve baseLine { get; set; }

  public virtual Level? level { get; internal set; }

  public string units { get; set; }

  [DetachProperty]
  public List<Mesh> displayValue { get; set; }
}
