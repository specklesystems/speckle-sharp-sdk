using Speckle.Core.Models;
using Speckle.Objects.Geometry;

namespace Speckle.Objects.BuiltElements;

public class CableTray : Base, IDisplayValue<List<Mesh>>
{
  public ICurve baseCurve { get; set; }
  public double width { get; set; }
  public double height { get; set; }
  public double length { get; set; }

  public string units { get; set; }

  [DetachProperty]
  public List<Mesh> displayValue { get; set; }
}
