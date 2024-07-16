using System.Collections.Generic;
using Objects.Geometry;
using Speckle.Core.Kits;
using Speckle.Core.Models;

namespace Objects.BuiltElements;

public class Brace : Base, IDisplayValue<IReadOnlyList<IBasicGeometryType>>
{
  public Brace() { }

  public Brace(ICurve baseLine, string? units, IReadOnlyList<Mesh>? displayValue = null)
  {
    this.baseLine = baseLine;
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

  public string? units { get; set; }

  [DetachProperty]
  public IReadOnlyList<IBasicGeometryType> displayValue { get; set; }

  [SchemaInfo("Brace", "Creates a Speckle brace", "BIM", "Structure")]
  public Brace([SchemaMainParam] ICurve baseLine)
    : this(baseLine, null) { }
}
