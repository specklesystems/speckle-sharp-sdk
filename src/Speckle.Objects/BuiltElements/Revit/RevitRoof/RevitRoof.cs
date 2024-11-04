using Speckle.Objects.Geometry;
using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements.Revit.RevitRoof;

[SpeckleType("Objects.BuiltElements.Revit.RevitRoof.RevitRoof")]
public class RevitRoof : Roof
{
  public string family { get; set; }
  public string type { get; set; }
  public Base? parameters { get; set; }
  public string elementId { get; set; }

  public new Level? level
  {
    get => base.level;
    set => base.level = value;
  }
}

[SpeckleType("Objects.BuiltElements.Revit.RevitRoof.RevitExtrusionRoof")]
public class RevitExtrusionRoof : RevitRoof
{
  public RevitExtrusionRoof() { }

  public double start { get; set; }
  public double end { get; set; }
  public Line referenceLine { get; set; }
}

[SpeckleType("Objects.BuiltElements.Revit.RevitRoof.RevitFootprintRoof")]
public class RevitFootprintRoof : RevitRoof
{
  public RevitFootprintRoof() { }

  public RevitLevel? cutOffLevel { get; set; }
  public double? slope { get; set; }
}
