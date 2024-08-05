using Speckle.Objects.Geometry;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements;

[SpeckleType("Objects.BuiltElements.Floor")]
public class Floor : Base, IDisplayValue<List<Mesh>>
{
  public Floor() { }

  [SchemaInfo("Floor", "Creates a Speckle floor", "BIM", "Architecture")]
  public Floor(
    [SchemaMainParam] ICurve outline,
    List<ICurve>? voids = null,
    [SchemaParamInfo("Any nested elements that this floor might have")] List<Base>? elements = null
  )
  {
    this.outline = outline;

    this.voids = voids ?? new();

    this.elements = elements;
  }

  public ICurve outline { get; set; }
  public List<ICurve> voids { get; set; } = new();

  [DetachProperty]
  public List<Base>? elements { get; set; }
  public virtual Level? level { get; internal set; }
  public string units { get; set; }

  [DetachProperty]
  public List<Mesh> displayValue { get; set; }
}
