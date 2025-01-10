using Speckle.Objects.Geometry;
using Speckle.Sdk.Models;

namespace Speckle.Objects.GIS;

//Supressing for now, class needs is yet to be decorated with `required` keywords
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

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
