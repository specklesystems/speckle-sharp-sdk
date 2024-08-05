using Speckle.Core.Models;
using Speckle.Objects.Geometry;

namespace Speckle.Objects.GIS;

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
