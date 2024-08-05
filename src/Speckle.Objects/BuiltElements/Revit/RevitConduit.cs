using Speckle.Objects.BuiltElements.Revit.Interfaces;
using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements.Revit;

public class RevitConduit : Conduit, IHasMEPConnectors
{
  public string family { get; set; }

  public string type { get; set; }

  public Level level { get; set; }

  public Base parameters { get; set; }

  public string elementId { get; set; }
  public List<RevitMEPConnector> Connectors { get; set; } = new();
}
