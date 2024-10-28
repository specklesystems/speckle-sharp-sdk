using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements.Revit;

[SpeckleType("Objects.BuiltElements.Revit.RevitColumn")]
public class RevitColumn : Column
{
  [DetachProperty]
  public Level? topLevel { get; set; }
  public double baseOffset { get; set; }
  public double topOffset { get; set; }
  public bool facingFlipped { get; set; }
  public bool handFlipped { get; set; }
  public double rotation { get; set; }
  public bool isSlanted { get; set; }
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
