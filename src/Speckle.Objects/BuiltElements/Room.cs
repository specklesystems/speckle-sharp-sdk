using Speckle.Objects.Geometry;
using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements;

[SpeckleType("Objects.BuiltElements.Room")]
public class Room : Base, IHasArea, IHasVolume, IDisplayValue<List<Mesh>>
{
  public Room() { }

  public string name { get; set; }
  public string number { get; set; }
  public virtual Level? level { get; set; }
  public Point basePoint { get; set; }
  public double height { get; set; }
  public List<ICurve> voids { get; set; } = new();
  public ICurve outline { get; set; }

  public string units { get; set; }

  [DetachProperty]
  public List<Mesh> displayValue { get; set; }

  public double area { get; set; }
  public double volume { get; set; }
}
