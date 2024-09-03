using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements.Revit;

[SpeckleType("Objects.BuiltElements.Revit.RevitTopography")]
public class RevitTopography : Topography
{
  public string elementId { get; set; }
  public Base? parameters { get; set; }
}
