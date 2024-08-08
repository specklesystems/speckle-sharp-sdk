using Speckle.Objects.Structural.Properties;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Structural.CSI.Properties;

[SpeckleType("Objects.Structural.CSI.Properties.CSITendonProperty")]
public class CSITendonProperty : Property1D
{
  public ModelingOption modelingOption { get; set; }
  public double Area { get; set; }
}
