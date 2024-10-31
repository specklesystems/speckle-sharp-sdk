using System.Diagnostics.CodeAnalysis;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Geometry.Revit;

[SpeckleType("Objects.Geometry.Revit.RevitLine")]
public class RevitLine : Line
{
  public RevitLine() { }

  [SetsRequiredMembers]
  public RevitLine(Line line)
  {
    start = line.start;
    end = line.end;
    units = line.units;
    domain = line.domain;
  }

  public string lineStyle { get; set; }
  public string elementId { get; set; }
}
