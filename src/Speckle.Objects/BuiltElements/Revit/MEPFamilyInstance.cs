using Speckle.Objects.BuiltElements.Revit.Interfaces;
using Speckle.Objects.Other.Revit;
using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements.Revit;

[SpeckleType("Objects.BuiltElements.Revit.RevitMEPFamilyInstance")]
public class RevitMEPFamilyInstance : RevitInstance, IHasMEPConnectors
{
  public string RevitPartType { get; set; }

  [DetachProperty]
  public List<RevitMEPConnector> Connectors { get; set; } = new();
  public List<ICurve> Curves { get; set; } = new();
}
