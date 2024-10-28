using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements;

[SpeckleType("Objects.BuiltElements.GridLine")]
public class GridLine : Base, IDisplayValue<List<Base>>
{
  public required ICurve baseLine { get; set; }
  public string label { get; set; }
  public string units { get; set; }

  [DetachProperty]
  public List<Base> displayValue { get; set; }
}
