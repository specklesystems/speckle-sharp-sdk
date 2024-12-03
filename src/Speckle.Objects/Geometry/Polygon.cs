using Speckle.Sdk.Models;

namespace Speckle.Objects.Geometry;

[SpeckleType("Objects.Geometry.Polygon")]
public class Polygon : Base
{
  public string units { get; set; }
  public required ICurve boundary { get; set; }
  public List<ICurve> innerLoops { get; set; }

  public Polygon()
  {
    innerLoops = new List<ICurve>();
  }
}
