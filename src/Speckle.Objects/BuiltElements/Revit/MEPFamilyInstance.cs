using Speckle.Core.Models;
using Speckle.Objects.BuiltElements.Revit.Interfaces;
using Speckle.Objects.Other.Revit;

namespace Speckle.Objects.BuiltElements.Revit;

public class RevitMEPFamilyInstance : RevitInstance, IHasMEPConnectors
{
  public string RevitPartType { get; set; }

  [DetachProperty]
  public List<RevitMEPConnector> Connectors { get; set; } = new();
  public List<ICurve> Curves { get; set; } = new();
}
