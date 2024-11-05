using Speckle.Objects.Geometry;
using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements.Revit;

[SpeckleType("Objects.BuiltElements.Revit.DirectShape")]
public class DirectShape : Base, IDisplayValue<List<Base>>
{
  public required string name { get; set; }
  public RevitCategory category { get; set; }
  public Base? parameters { get; set; }
  public string elementId { get; set; }

  [DetachProperty]
  public List<Base> baseGeometries { get; set; } = new();

  public string units { get; set; }

  [DetachProperty]
  public List<Base> displayValue { get; set; }

  public bool IsValidObject(Base @base)
  {
    return @base is Point || @base is ICurve || @base is Mesh || @base is Brep;
  }
}
