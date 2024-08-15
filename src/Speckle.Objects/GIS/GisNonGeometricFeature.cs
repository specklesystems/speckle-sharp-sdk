using Speckle.Sdk.Models;

namespace Speckle.Objects.GIS;

[SpeckleType("Objects.GIS.GisNonGeometricFeature")]
public class GisNonGeometricFeature : Base, IGisFeature
{
  public required Base attributes { get; set; }
}
