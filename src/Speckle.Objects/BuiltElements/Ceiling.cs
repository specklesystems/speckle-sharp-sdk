using Speckle.Objects.Geometry;
using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements;

[SpeckleType("Objects.BuiltElements.Ceiling")]
public class Ceiling : Base, IDisplayValue<List<Mesh>>
{
  public ICurve outline { get; set; }
  public List<ICurve> voids { get; set; } = new();

  [DetachProperty]
  public List<Base>? elements { get; set; }

  public string units { get; set; }

  [DetachProperty]
  public List<Mesh> displayValue { get; set; }
}
