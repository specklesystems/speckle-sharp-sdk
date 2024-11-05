using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements.Revit;

[SpeckleType("Objects.BuiltElements.Revit.RevitLevel")]
public class RevitLevel : Level
{
  public RevitLevel() { }

  public bool createView { get; set; }
  public Base? parameters { get; set; }
  public string elementId { get; set; }
  public bool referenceOnly { get; set; }
}
