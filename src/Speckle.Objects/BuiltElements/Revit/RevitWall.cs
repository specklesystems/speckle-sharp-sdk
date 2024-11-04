using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements.Revit;

[SpeckleType("Objects.BuiltElements.Revit.RevitWall")]
public class RevitWall : Wall
{
  public required string family { get; set; }
  public required string type { get; set; }
  public double baseOffset { get; set; }
  public double topOffset { get; set; }
  public bool flipped { get; set; }
  public bool structural { get; set; }

  [DetachProperty]
  public Level? topLevel { get; set; }
  public Base? parameters { get; set; }
  public string? elementId { get; set; }

  public new Level? level
  {
    get => base.level;
    set => base.level = value;
  }
}
