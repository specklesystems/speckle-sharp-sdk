using Speckle.Objects.Geometry;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements;

public class Brace : Base, IDisplayValue<IReadOnlyList<Base>>
{
  public Brace() { }

  public Brace(ICurve baseLine, string? units, IReadOnlyList<Mesh>? displayValue = null)
  {
    this.baseLine = baseLine;
    this.units = units;
    this.displayValue = ((IReadOnlyList<Base>?)displayValue) ?? new[] { (Base)baseLine };
  }

  public ICurve baseLine { get; set; }

  public string? units { get; set; }

  [DetachProperty]
  public IReadOnlyList<Base> displayValue { get; set; }

  [SchemaInfo("Brace", "Creates a Speckle brace", "BIM", "Structure")]
  public Brace([SchemaMainParam] ICurve baseLine)
    : this(baseLine, null) { }
}
