using Speckle.Objects.Geometry;
using Speckle.Objects.Structural.Geometry;
using Speckle.Objects.Structural.GSA.Geometry;
using Speckle.Objects.Structural.Loading;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Structural.GSA.Loading;

[SpeckleType("Objects.Structural.GSA.Loading.GSALoadGridPoint")]
public class GSALoadGridPoint : GSALoadGrid
{
  public GSALoadGridPoint() { }

  public GSALoadGridPoint(
    int nativeId,
    GSAGridSurface gridSurface,
    Axis loadAxis,
    LoadDirection2D direction,
    Point position,
    double value
  )
  {
    this.nativeId = nativeId;
    this.gridSurface = gridSurface;
    this.loadAxis = loadAxis;
    this.direction = direction;
    this.position = position;
    this.value = value;
  }

  public Point position { get; set; }
  public double value { get; set; }
}
