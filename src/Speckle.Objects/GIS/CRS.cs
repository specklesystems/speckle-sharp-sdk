using Speckle.Sdk.Models;

namespace Speckle.Objects.GIS;

[SpeckleType("Objects.GIS.CRS")]
public class CRS : Base
{
  public required string name { get; set; }
  public required string? authority_id { get; set; }
  public required string wkt { get; set; }
  public string? units_native { get; set; }
  public float? offset_x { get; set; }
  public float? offset_y { get; set; }
  public float? rotation { get; set; }
}
