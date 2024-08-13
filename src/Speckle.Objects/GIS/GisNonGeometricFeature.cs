using Speckle.Sdk.Models;

namespace Speckle.Objects.GIS;

[SpeckleType("Objects.GIS.GisNonGeometricFeature")]
public class GisNonGeometricFeature : Base, IGisFeature
{
  public GisNonGeometricFeature() { }

  public GisNonGeometricFeature(Base attributes)
  {
    this.attributes = attributes;
  }

  public Base attributes { get; set; }
}
