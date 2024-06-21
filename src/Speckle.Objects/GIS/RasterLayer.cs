using Speckle.Core.Models;

namespace Speckle.Objects.GIS;

public class RasterLayer : Collection
{
  public CRS? crs { get; set; }
  public string? units { get; set; }
  public CRS? rasterCrs { get; set; }
  public string? geomType { get; set; }
  public Dictionary<string, object>? renderer { get; set; }

  public RasterLayer()
  {
    collectionType = "RasterLayer";
  }
}
