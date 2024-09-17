using Speckle.Objects.Geometry;
using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements;

[SpeckleType("Objects.BuiltElements.Topography")]
public class Topography : Base, IDisplayValue<List<Mesh>>
{
  public required Mesh baseGeometry { get; set; }
  public required string units { get; set; }

  [DetachProperty]
  public required List<Mesh> displayValue { get; set; }
}
