using Speckle.Objects.Geometry;
using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements.Revit;

[SpeckleType("Objects.BuiltElements.Revit.RevitFloor")]
public class RevitFloor : Floor
{
  public required string family { get; set; }
  public required string type { get; set; }

  public new Level? level
  {
    get => base.level;
    set => base.level = value;
  }

  public bool structural { get; set; }
  public double slope { get; set; }
  public Line? slopeDirection { get; set; }
  public Base? parameters { get; set; }
  public string elementId { get; set; }
}
