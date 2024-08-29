using Speckle.Objects.Geometry;
using Speckle.Sdk.Models;

namespace Speckle.Objects.GIS;

[SpeckleType("Objects.GIS.PolygonGeometry")]
public class PolygonGeometry : Base
{
  public string units { get; set; }
  public Polyline boundary { get; set; }
  public List<Polyline> voids { get; set; }

  public PolygonGeometry()
  {
    voids = new List<Polyline>();
  }
}
