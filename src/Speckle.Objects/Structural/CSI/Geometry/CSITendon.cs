using Speckle.Objects.Geometry;
using Speckle.Objects.Structural.CSI.Properties;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Structural.CSI.Geometry;

[SpeckleType("Objects.Structural.CSI.Geometry.CSITendon")]
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
