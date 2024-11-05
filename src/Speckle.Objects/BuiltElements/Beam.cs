using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements;

[SpeckleType("Objects.BuiltElements.Beam")]
public class Beam : Base, IDisplayValue<IReadOnlyList<Base>>
{
  public required ICurve baseLine { get; set; }

  public virtual Level? level { get; internal set; }

  public string? units { get; set; }

  [DetachProperty]
  public IReadOnlyList<Base> displayValue { get; set; }
}
