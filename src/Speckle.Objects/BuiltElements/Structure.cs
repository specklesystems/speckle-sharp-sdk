using Speckle.Core.Models;
using Speckle.Objects.Geometry;

namespace Speckle.Objects.BuiltElements;

public class Structure : Base, IDisplayValue<List<Mesh>>
{
  public Point location { get; set; }
  public List<string> pipeIds { get; set; }

  public string units { get; set; }

  [DetachProperty]
  public List<Mesh> displayValue { get; set; }
}
