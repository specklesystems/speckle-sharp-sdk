using Speckle.Objects.Geometry;
using Speckle.Objects.Utils;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements.Revit;

[SpeckleType("Objects.BuiltElements.Revit.AdaptiveComponent")]
public class AdaptiveComponent : Base, IDisplayValue<List<Mesh>>
{
  public AdaptiveComponent() { }

  [SchemaInfo("AdaptiveComponent", "Creates a Revit adaptive component by points", "Revit", "Families")]
  public AdaptiveComponent(
    string type,
    string family,
    List<Point> basePoints,
    bool flipped = false,
    List<Parameter>? parameters = null
  )
  {
    this.type = type;
    this.family = family;
    this.basePoints = basePoints;
    this.flipped = flipped;
    this.parameters = parameters?.ToBase();
  }

  public string type { get; set; }
  public string family { get; set; }
  public List<Point> basePoints { get; set; }
  public bool flipped { get; set; }
  public string elementId { get; set; }
  public Base? parameters { get; set; }

  public string units { get; set; }

  [DetachProperty]
  public List<Mesh> displayValue { get; set; }
}
