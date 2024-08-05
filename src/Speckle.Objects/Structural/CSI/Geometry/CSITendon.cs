using Speckle.Core.Models;
using Speckle.Objects.Geometry;
using Speckle.Objects.Structural.CSI.Properties;

namespace Speckle.Objects.Structural.CSI.Geometry;

public class CSITendon : CSIElement1D
{
  public CSITendon(string name, Polycurve polycurve, CSITendonProperty CSITendonProperty)
  {
    this.name = name;
    this.polycurve = polycurve;
    this.CSITendonProperty = CSITendonProperty;
  }

  public CSITendon() { }

  public Polycurve polycurve { get; set; }

  [DetachProperty]
  public CSITendonProperty CSITendonProperty { get; set; }
}
