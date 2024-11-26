using Speckle.Sdk.Models;

namespace Speckle.Objects.Geometry;

[SpeckleType("Objects.Geometry.Polygon")]
public class Polygon : Base
{
  public string units { get; set; }
  public ICurve boundary { get; set; }
  public List<ICurve> voids { get; set; }

  public Polygon()
  {
    voids = new List<ICurve>();
  }
}
