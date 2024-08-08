using Speckle.Objects.Geometry;
using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements.Revit;

[SpeckleType("Objects.BuiltElements.Revit.StructuralConnectionHandler")]
public class StructuralConnectionHandler : Base, IDisplayValue<List<Mesh>>
{
  public string family { get; set; }
  public string type { get; set; }
  public Point basePoint { get; set; }

  [DetachProperty]
  public List<Base> connectedElements { get; set; }

  [DetachProperty]
  public List<Mesh> displayValue { get; set; }
}
