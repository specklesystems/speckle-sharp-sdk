using Speckle.Sdk.Models;

namespace Speckle.Objects.GIS;

[SpeckleType("Objects.GIS.GisMultipatchGeometry")]
public class GisMultipatchGeometry : Base
{
  public required string units { get; set; }
  public required List<int> faces { get; set; }
  public required List<double> vertices { get; set; }
  public List<int>? colors { get; set; }
}
