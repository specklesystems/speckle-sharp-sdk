using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements.Revit;

[SpeckleType("Objects.BuiltElements.Revit.RevitBeam")]
public class RevitBeam : Beam
{
  public required string family { get; set; }
  public required string type { get; set; }
  public Base? parameters { get; set; }
  public string? elementId { get; set; }

  public new Level? level
  {
    get => base.level;
    set => base.level = value;
  }
}
