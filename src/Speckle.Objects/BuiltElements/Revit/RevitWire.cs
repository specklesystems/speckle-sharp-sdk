using Speckle.Objects.BuiltElements.Revit.Interfaces;
using Speckle.Objects.Utils;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements.Revit;

[SpeckleType("Objects.BuiltElements.Revit.RevitWire")]
public class RevitWire : Wire, IHasMEPConnectors
{
  public RevitWire() { }

  [SchemaInfo("RevitWire", "Creates a Revit wire from points and level", "Revit", "MEP")]
  public RevitWire(
    List<double> constructionPoints,
    string family,
    string type,
    Level level,
    string wiringType = "Arc",
    List<Parameter>? parameters = null
  )
  {
    this.constructionPoints = constructionPoints;
    this.family = family;
    this.type = type;
    this.level = level;
    this.wiringType = wiringType;
    this.parameters = parameters?.ToBase();
  }

  public string family { get; set; }
  public string type { get; set; }
  public string wiringType { get; set; }
  public List<double> constructionPoints { get; set; } // used in constructor for revit native wires
  public string system { get; set; }

  [DetachProperty]
  public Level level { get; set; }
  public Base? parameters { get; set; }
  public string elementId { get; set; }
  public List<RevitMEPConnector> Connectors { get; set; } = new();
}
