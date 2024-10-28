using Speckle.Objects.Geometry;
using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements.Revit;

[SpeckleType("Objects.BuiltElements.Revit.RevitCeiling")]
public class RevitCeiling : Ceiling
{
  public RevitCeiling() { }

  public required string family { get; set; }
  public required string type { get; set; }

  [DetachProperty]
  public Level level { get; set; }
  public double slope { get; set; }
  public Line? slopeDirection { get; set; }

  [Obsolete("Offset property is now captured in parameters to match the behavior of other Revit objects", true)]
  public double offset { get; set; }

  public Base? parameters { get; set; }
  public string elementId { get; set; }
}
