using Speckle.Objects.Geometry;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements;

public class Beam : Base, IDisplayValue<IReadOnlyList<Base>>
{
  public Beam() { }

  public Beam(ICurve baseLine, Level? level, string? units, IReadOnlyList<Mesh>? displayValue = null)
  {
    this.baseLine = baseLine;
    this.level = level;
    this.units = units;
    this.displayValue = ((IReadOnlyList<Base>?)displayValue) ?? new[] { (Base)baseLine };
  }

  public ICurve baseLine { get; set; }

  public virtual Level? level { get; internal set; }

  public string? units { get; set; }

  [DetachProperty]
  public IReadOnlyList<Base> displayValue { get; set; }

  #region Schema Info Constructors
  [SchemaInfo("Beam", "Creates a Speckle beam", "BIM", "Structure")]
  public Beam([SchemaMainParam] ICurve baseLine)
    : this(baseLine, null, null) { }

  #endregion
}
