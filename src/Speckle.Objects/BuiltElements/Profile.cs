using Speckle.Objects.Geometry;
using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements;

[SpeckleType("Objects.BuiltElements.Profile")]
public class Profile : Base, IDisplayValue<Polyline>
{
  public List<ICurve> curves { get; set; }

  public string name { get; set; }

  public double startStation { get; set; }

  public double endStation { get; set; }

  public string units { get; set; }

  [DetachProperty]
  public Polyline displayValue { get; set; }
}
