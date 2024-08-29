using Speckle.Objects.Geometry;
using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements.Revit;

[SpeckleType("Objects.BuiltElements.Revit.RevitMEPConnector")]
public class RevitMEPConnector : Base
{
  public double angle { get; set; }
  public List<string> connectedConnectorIds { get; set; } = new();
  public double height { get; set; }
  public Point origin { get; set; }
  public double radius { get; set; }
  public string shape { get; set; }
  public string systemName { get; set; }
  public double width { get; set; }
}
