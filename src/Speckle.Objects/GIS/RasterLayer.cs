using Speckle.Sdk.Models;
using Speckle.Sdk.Models.Collections;

namespace Speckle.Objects.GIS;

[SpeckleType("Objects.GIS.RasterLayer")]
public class RasterLayer : Collection
{
  public CRS? crs { get; set; }
  public string? units { get; set; }
  public CRS? rasterCrs { get; set; }
  public string? geomType { get; set; }
  public Dictionary<string, object>? renderer { get; set; }
  
}
