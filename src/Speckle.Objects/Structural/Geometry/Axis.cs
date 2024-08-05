using Speckle.Objects.Geometry;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Structural.Geometry;

public class Axis : Base
{
  public Axis() { }

  [SchemaInfo("Axis", "Creates a Speckle structural axis (a user-defined axis)", "Structural", "Geometry")]
  public Axis(string name, AxisType axisType = AxisType.Cartesian, Plane? definition = null)
  {
    this.name = name;
    this.axisType = axisType;
    this.definition = definition;
  }

  public string name { get; set; }
  public AxisType axisType { get; set; }
  public Plane? definition { get; set; }
}
