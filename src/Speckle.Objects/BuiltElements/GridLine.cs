using Speckle.Sdk.Host;
using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements;

[SpeckleType("Objects.BuiltElements.GridLine")]
public class GridLine : Base, IDisplayValue<List<Base>>
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
  public List<Base> displayValue { get; set; }
}
