using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements.TeklaStructures;

[SpeckleType("Objects.BuiltElements.TeklaStructures.TeklaOpening")]
public class TeklaOpening : Opening
{
  public string openingHostId { get; set; }
  public TeklaOpeningTypeEnum openingType { get; set; }
}

[SpeckleType("Objects.BuiltElements.TeklaStructures.TeklaContourOpening")]
public class TeklaContourOpening : TeklaOpening
{
  public TeklaContourPlate cuttingPlate { get; set; }
  public double thickness { get; set; }
}

[SpeckleType("Objects.BuiltElements.TeklaStructures.TeklaBeamOpening")]
public class TeklaBeamOpening : TeklaOpening
{
  public TeklaBeam cuttingBeam { get; set; }
}
