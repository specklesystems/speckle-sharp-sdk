using System.Collections.Generic;
using Objects.Geometry;
using Speckle.Core.Kits;
using Speckle.Core.Models;

namespace Objects.BuiltElements;

public class Beam : Base, IDisplayValue<IReadOnlyList<IBasicGeometryType>>
{
  public Beam() { }

  public Beam(ICurve baseLine, Level? level, string? units, IReadOnlyList<Mesh>? displayValue = null)
  {
    this.baseLine = baseLine;
    this.level = level;
    this.units = units;
    IReadOnlyList<IBasicGeometryType>? calculatedDisplayValue = displayValue;
    if (displayValue is null)
    {
      switch (baseLine)
      {
        case Line l:
          calculatedDisplayValue = new[] { l };
          break;
        case Curve c:
          calculatedDisplayValue = c.displayValue;
          break;
      }
    }

    if (calculatedDisplayValue is not null)
    {
      this.displayValue = calculatedDisplayValue;
    }
  }

  public ICurve baseLine { get; set; }

  public virtual Level? level { get; internal set; }

  public string? units { get; set; }

  [DetachProperty]
  public IReadOnlyList<IBasicGeometryType> displayValue { get; set; }

  #region Schema Info Constructors
  [SchemaInfo("Beam", "Creates a Speckle beam", "BIM", "Structure")]
  public Beam([SchemaMainParam] ICurve baseLine)
    : this(baseLine, null, null) { }

  #endregion
}
