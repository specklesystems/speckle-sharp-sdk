using Speckle.Objects.Geometry;
using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements;

[SpeckleType("Objects.BuiltElements.Roof")]
public class Roof : Base, IDisplayValue<List<Mesh>>
{
  public ICurve outline { get; set; }
  public virtual Level? level { get; internal set; }
  public List<ICurve> voids { get; set; } = new();

  [DetachProperty]
  public List<Base>? elements { get; set; }

  public string units { get; set; }

  [DetachProperty]
  public List<Mesh> displayValue { get; set; }
}
