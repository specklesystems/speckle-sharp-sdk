using Speckle.Objects.Structural.Geometry;
using Speckle.Objects.Structural.GSA.Geometry;
using Speckle.Objects.Structural.Loading;

namespace Speckle.Objects.Structural.GSA.Loading;

public abstract class GSALoadGrid : Load
{
  protected GSALoadGrid() { }

  protected GSALoadGrid(int nativeId, GSAGridSurface gridSurface, Axis loadAxis, LoadDirection2D direction)
  {
    this.nativeId = nativeId;
    this.gridSurface = gridSurface;
    this.loadAxis = loadAxis;
    this.direction = direction;
  }

  public int nativeId { get; set; }
  public GSAGridSurface gridSurface { get; set; }
  public Axis loadAxis { get; set; }
  public LoadDirection2D direction { get; set; }
}
