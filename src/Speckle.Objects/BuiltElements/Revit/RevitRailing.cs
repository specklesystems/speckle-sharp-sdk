using Speckle.Objects.Geometry;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements.Revit;

[SpeckleType("Objects.BuiltElements.Revit.RevitRailing")]
public class RevitRailing : Base, IDisplayValue<List<Mesh>>
{
  public RevitRailing() { }

  [SchemaInfo("Railing", "Creates a Revit railing by base curve.", "Revit", "Architecture")]
  public RevitRailing(string type, [SchemaMainParam] Polycurve baseCurve, Level level, bool flipped = false)
  {
    this.type = type;
    path = baseCurve;
    this.level = level;
    this.flipped = flipped;
  }

  //public string family { get; set; }
  public string type { get; set; }

  [DetachProperty]
  public Level level { get; set; }
  public Polycurve path { get; set; }
  public bool flipped { get; set; }
  public string elementId { get; set; }
  public Base parameters { get; set; }
  public RevitTopRail topRail { get; set; }

  public string units { get; set; }

  [DetachProperty]
  public List<Mesh> displayValue { get; set; }
}

// Used only to transfer parameters of the top railing
// its display mesh will live in the main railing element
[SpeckleType("Objects.BuiltElements.Revit.RevitTopRail")]
public class RevitTopRail : Base
{
  //public string family { get; set; }
  public string type { get; set; }
  public string elementId { get; set; }
  public Base parameters { get; set; }

  [DetachProperty]
  public List<Mesh> displayValue { get; set; }

  public string units { get; set; }
}
