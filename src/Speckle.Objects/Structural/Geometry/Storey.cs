using Speckle.Objects.BuiltElements;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Structural.Geometry;

[SpeckleType("Objects.Structural.Geometry.Storey")]
public class Storey : Level // or inherit from Base?
{
  public Storey() { }

  /// <summary>
  /// A storey in the structural model
  /// </summary>
  /// <param name="name">The name of the storey</param>
  /// <param name="elevation">The elevation of the storey (along the global z-axis, ie. storey exists in the global XY plane)</param>
  [SchemaInfo(
    "Storey",
    "Creates a Speckle structural storey (to describe floor levels/storeys in the structural model)",
    "Structural",
    "Geometry"
  )]
  public Storey(string name, double elevation)
  {
    this.name = name;
    this.elevation = elevation;
  }
}
