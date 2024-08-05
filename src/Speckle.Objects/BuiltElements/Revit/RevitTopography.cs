using Speckle.Objects.Geometry;
using Speckle.Objects.Utils;
using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements.Revit;

public class RevitTopography : Topography
{
  public RevitTopography() { }

  [SchemaInfo("RevitTopography", "Creates a Revit topography", "Revit", "Architecture")]
  public RevitTopography([SchemaMainParam] Mesh displayMesh, List<Parameter>? parameters = null)
  {
    displayValue = new List<Mesh> { displayMesh };
    this.parameters = parameters?.ToBase();
  }

  public string elementId { get; set; }
  public Base? parameters { get; set; }
}
