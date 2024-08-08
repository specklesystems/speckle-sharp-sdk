using Speckle.Sdk.Models;

namespace Speckle.Objects.GIS;

[SpeckleType("Objects.GIS.GisMultipatchGeometry")]
public class GisMultipatchGeometry : Base
{
  public string units { get; set; }
  public List<int> faces { get; set; }
  public List<double> vertices { get; set; }
  public List<int>? colors { get; set; }

  public GisMultipatchGeometry()
  {
    faces = new List<int>();
    vertices = new List<double>();
  }
}
