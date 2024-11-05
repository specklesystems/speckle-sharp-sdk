using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements.Revit;

[SpeckleType("Objects.BuiltElements.Revit.RevitBrace")]
public class RevitBrace : Brace
{
  public required string family { get; set; }
  public required string type { get; set; }
  public Base? parameters { get; set; }
  public string? elementId { get; set; }

  [DetachProperty]
  public Level? level { get; set; }
}
