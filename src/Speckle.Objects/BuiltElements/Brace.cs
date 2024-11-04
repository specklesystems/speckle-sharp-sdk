using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements;

[SpeckleType("Objects.BuiltElements.Brace")]
public class Brace : Base, IDisplayValue<IReadOnlyList<Base>>
{
  public required ICurve baseLine { get; set; }

  public string? units { get; set; }

  [DetachProperty]
  public IReadOnlyList<Base> displayValue { get; set; }
}
