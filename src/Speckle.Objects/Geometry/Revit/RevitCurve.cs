using System.Diagnostics.CodeAnalysis;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Geometry.Revit;

[SpeckleType("Objects.Geometry.Revit.RevitCurve")]
public class RevitCurve : Curve
{
  public RevitCurve() { }

  [SetsRequiredMembers]
  public RevitCurve(Curve curve)
  {
    area = curve.area;
    closed = curve.closed;
    degree = curve.degree;
    displayValue = curve.displayValue;
    domain = curve.domain;
    knots = curve.knots;
    length = curve.length;
    periodic = curve.periodic;
    points = curve.points;
    rational = curve.rational;
    units = curve.units;
    weights = curve.weights;
  }

  public string lineStyle { get; set; }
  public string elementId { get; set; }
}
