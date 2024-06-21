using Speckle.Core.Models;
using Speckle.Objects.Geometry;

namespace Speckle.Objects.BuiltElements.Revit;

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
