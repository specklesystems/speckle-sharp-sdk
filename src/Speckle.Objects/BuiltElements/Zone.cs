using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements;

[SpeckleType("Objects.BuiltElements.Zone")]
public class Zone : Base, IHasArea, IHasVolume
{
  public Zone() { }

  public Zone(string name)
  {
    this.name = name;
  }

  public string name { get; set; }
  public string units { get; set; }

  public List<Space> spaces { get; set; }

  // implicit measurements
  public double area { get; set; }
  public double volume { get; set; }
  public double perimeter { get; set; }
}
