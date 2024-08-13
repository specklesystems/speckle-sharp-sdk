using Speckle.Sdk.Models;

namespace Speckle.Objects.GIS;

[SpeckleType("Objects.GIS.GisRecordFeature")]
public class GisRecordFeature : Base, IGisFeature
{
  public GisRecordFeature() { }

  public GisRecordFeature(Base attributes)
  {
    this.attributes = attributes;
  }

  public Base attributes { get; set; }
}
