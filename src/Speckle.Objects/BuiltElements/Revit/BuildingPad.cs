using Speckle.Objects.Geometry;
using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements.Revit;

[SpeckleType("Objects.BuiltElements.Revit.BuildingPad")]
public class BuildingPad : Base, IDisplayValue<List<Mesh>>
{
  public ICurve outline { get; set; }

  public List<ICurve> voids { get; set; } = new();

  public string type { get; set; }

  [DetachProperty]
  public Level level { get; set; }

  public Base parameters { get; set; }

  public string elementId { get; set; }

  public string units { get; set; }

  [DetachProperty]
  public List<Mesh> displayValue { get; set; }
}
