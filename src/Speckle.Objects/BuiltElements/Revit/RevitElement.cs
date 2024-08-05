using Speckle.Objects.Geometry;
using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements.Revit;

/// <summary>
/// A generic Revit element for which we don't have direct conversions
/// </summary>
[SpeckleType("Objects.BuiltElements.Revit.RevitElement")]
public class RevitElement : Base, IDisplayValue<List<Mesh>>
{
  public string family { get; set; }
  public string type { get; set; }
  public string category { get; set; }
  public Base parameters { get; set; }
  public string elementId { get; set; }

  [DetachProperty]
  public List<Mesh> displayValue { get; set; }
}
