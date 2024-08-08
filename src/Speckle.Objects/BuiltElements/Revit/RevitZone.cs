using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements.Revit;

[SpeckleType("Objects.BuiltElements.Revit.RevitZone")]
public class RevitZone : Zone
{
  public RevitZone() { }

  public Level level { get; set; }
  public string phaseName { get; set; }
  public Base parameters { get; set; }
  public string elementId { get; set; }
  public bool isDefault { get; set; }
  public string serviceType { get; set; }
}
