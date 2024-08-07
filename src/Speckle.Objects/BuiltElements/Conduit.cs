using Speckle.Objects.Geometry;
using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements;

[SpeckleType("Objects.BuiltElements.Conduit")]
public class Conduit : Base, IDisplayValue<List<Mesh>>
{
  public ICurve baseCurve { get; set; }
  public double diameter { get; set; }
  public double length { get; set; }

  public string units { get; set; }

  [DetachProperty]
  public List<Mesh> displayValue { get; set; }
}
