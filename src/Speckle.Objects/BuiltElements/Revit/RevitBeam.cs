using Speckle.Objects.Geometry;
using Speckle.Objects.Utils;
using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements.Revit;

public class RevitBeam : Beam
{
  public RevitBeam() { }

  public RevitBeam(
    string family,
    string type,
    ICurve baseLine,
    Level? level,
    string? units,
    List<Mesh>? displayValue = null,
    List<Parameter>? parameters = null
  )
    : base(baseLine, level, units, displayValue)
  {
    this.family = family;
    this.type = type;
    this.parameters = parameters?.ToBase();
  }

  public string family { get; set; }
  public string type { get; set; }
  public Base? parameters { get; set; }
  public string elementId { get; set; }

  public new Level? level
  {
    get => base.level;
    set => base.level = value;
  }

  #region Schema Info Constructors

  [SchemaInfo("RevitBeam", "Creates a Revit beam by curve and base level.", "Revit", "Structure")]
  public RevitBeam(
    string family,
    string type,
    [SchemaMainParam] ICurve baseLine,
    Level level,
    List<Parameter>? parameters = null
  )
    : this(family, type, baseLine, level, null, parameters: parameters) { }

  #endregion
}
