using Speckle.Objects.Geometry;
using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements;

[SpeckleType("Objects.BuiltElements.Floor")]
public class Floor : Base, IDisplayValue<List<Mesh>>
{
  public ICurve outline { get; set; }
  public List<ICurve> voids { get; set; } = new();

  [DetachProperty]
  public List<Base>? elements { get; set; }
  public virtual Level? level { get; internal set; }
  public string units { get; set; }

  [DetachProperty]
  public List<Mesh> displayValue { get; set; }
}
