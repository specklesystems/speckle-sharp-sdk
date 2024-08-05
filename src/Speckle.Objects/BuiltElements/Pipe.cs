using Speckle.Objects.Geometry;
using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements;

public class Pipe : Base, IDisplayValue<List<Mesh>>
{
  public Pipe() { }

  [SchemaInfo("Pipe", "Creates a Speckle pipe", "BIM", "MEP")]
  public Pipe(
    [SchemaMainParam] ICurve baseCurve,
    double length,
    double diameter,
    double flowrate = 0,
    double relativeRoughness = 0
  )
  {
    this.baseCurve = baseCurve;
    this.length = length;
    this.diameter = diameter;
    this["flowRate"] = flowrate;
    this["relativeRoughness"] = relativeRoughness;
  }

  public ICurve baseCurve { get; set; }
  public double length { get; set; }
  public double diameter { get; set; }

  public string units { get; set; }

  [DetachProperty]
  public List<Mesh> displayValue { get; set; }
}
