using Speckle.Objects.Geometry;
using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements;

[SpeckleType("Objects.BuiltElements.Structure")]
public class Structure : Base, IDisplayValue<List<Mesh>>
{
  public Point location { get; set; }
  public List<string> pipeIds { get; set; }

  public string units { get; set; }

  [DetachProperty]
  public List<Mesh> displayValue { get; set; }
}
