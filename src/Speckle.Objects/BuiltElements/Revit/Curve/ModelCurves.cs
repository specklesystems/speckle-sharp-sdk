using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements.Revit.Curve;

[SpeckleType("Objects.BuiltElements.Revit.Curve.ModelCurve")]
public class ModelCurve : Base
{
  public ModelCurve() { }

  public required ICurve baseCurve { get; set; }
  public string lineStyle { get; set; }
  public Base? parameters { get; set; }
  public string elementId { get; set; }

  public string units { get; set; }
}
