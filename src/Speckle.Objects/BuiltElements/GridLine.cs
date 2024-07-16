using System.Collections.Generic;
using Speckle.Core.Kits;
using Speckle.Core.Models;

namespace Objects.BuiltElements;

public class GridLine : Base, IDisplayValue<IEnumerable<IBasicGeometryType>>
{
  public GridLine() { }

  [SchemaInfo("GridLine", "Creates a Speckle grid line", "BIM", "Other"), SchemaDeprecated]
  public GridLine(
    [SchemaParamInfo("NOTE: only Line and Arc curves are supported in Revit"), SchemaMainParam] ICurve baseLine
  )
  {
    this.baseLine = baseLine;
  }

  [SchemaInfo("GridLine", "Creates a Speckle grid line with a label", "BIM", "Other")]
  public GridLine(
    [SchemaParamInfo("NOTE: only Line and Arc curves are supported in Revit"), SchemaMainParam] ICurve baseLine,
    string label = ""
  )
  {
    this.baseLine = baseLine;
    this.label = label;
  }

  public ICurve baseLine { get; set; }
  public string label { get; set; }

  public string units { get; set; }

  [DetachProperty]
  public IEnumerable<IBasicGeometryType> displayValue { get; set; }
}
