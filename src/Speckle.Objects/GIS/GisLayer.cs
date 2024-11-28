using Speckle.Sdk.Models;
using Speckle.Sdk.Models.Collections;

namespace Speckle.Objects.GIS;

[SpeckleType("Objects.GIS.GisLayer")]
public class GisLayer : Collection
{
  public required CRS crs { get; set; }
  public required string units { get; set; }
  public required string type { get; set; }
}
