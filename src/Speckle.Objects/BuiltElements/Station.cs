using Speckle.Objects.Geometry;
using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements;

[SpeckleType("Objects.BuiltElements.Station")]
public class Station : Base
{
  public double number { get; set; }
  public string type { get; set; }
  public Point location { get; set; }

  public string units { get; set; }
}
