using Speckle.Objects.Geometry;
using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements;

public class Topography : Base, IDisplayValue<List<Mesh>>
{
  public Topography()
  {
    displayValue = new List<Mesh>();
  }

  [SchemaInfo("Topography", "Creates a Speckle topography", "BIM", "Architecture")]
  public Topography([SchemaMainParam] Mesh displayMesh)
  {
    displayValue = new List<Mesh> { displayMesh };
  }

  public Mesh baseGeometry { get; set; } = new();

  public string units { get; set; }

  [DetachProperty]
  public List<Mesh> displayValue { get; set; }
  //TODO Figure out if we should add a new constructor that takes a List<Mesh> or if Topography should just have a single mesh display value
}
