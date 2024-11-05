using Speckle.Sdk.Models;
using Speckle.Sdk.Models.Collections;

namespace Speckle.Objects.GIS;

[SpeckleType("Objects.GIS.VectorLayer")]
public class VectorLayer : Collection
{
  public CRS? crs { get; set; }
  public string? units { get; set; }
  public Base attributes { get; set; }
  public string? geomType { get; set; }
  public string? nativeGeomType { get; set; }
  public Dictionary<string, object>? renderer { get; set; }

  public VectorLayer()
  {
    attributes = new Base();
  }
}
