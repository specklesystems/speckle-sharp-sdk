using System.Diagnostics.CodeAnalysis;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Geometry.Revit;

[SpeckleType("Objects.Geometry.Revit.RevitArc")]
public class RevitArc : Arc
{
  public RevitArc() { }

  [SetsRequiredMembers]
  public RevitArc(Arc arc)
  {
    startPoint = arc.startPoint;
    midPoint = arc.midPoint;
    endPoint = arc.endPoint;
    units = arc.units;
  }

  public string lineStyle { get; set; }
  public string elementId { get; set; }
}
