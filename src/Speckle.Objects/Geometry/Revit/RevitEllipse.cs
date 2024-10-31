using System.Diagnostics.CodeAnalysis;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Geometry.Revit;

[SpeckleType("Objects.Geometry.Revit.RevitEllipse")]
public class RevitEllipse : Ellipse
{
  public RevitEllipse() { }

  [SetsRequiredMembers]
  public RevitEllipse(Ellipse curve)
  {
    area = curve.area;
    firstRadius = curve.firstRadius;
    secondRadius = curve.secondRadius;
    domain = curve.domain;
    plane = curve.plane;
    length = curve.length;
    trimDomain = curve.trimDomain;
    units = curve.units;
  }

  public string lineStyle { get; set; }
  public string elementId { get; set; }
}
