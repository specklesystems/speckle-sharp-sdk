using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements;

[SpeckleType("Objects.BuiltElements.Wall")]
public class Wall : Base, IDisplayValue<IReadOnlyList<Base>>
{
  public double height { get; set; }

  public string? units { get; set; }
  public ICurve baseLine { get; set; }

  [DetachProperty]
  public virtual Level? level { get; internal set; }

  [DetachProperty]
  public List<Base>? elements { get; set; }

  [DetachProperty]
  public IReadOnlyList<Base> displayValue { get; set; }
}
