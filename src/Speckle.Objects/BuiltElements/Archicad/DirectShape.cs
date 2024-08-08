using Speckle.Objects.Geometry;
using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements.Archicad;

[SpeckleType("Objects.BuiltElements.Archicad.DirectShape")]
public class DirectShape : Base
{
  public DirectShape() { }

  public DirectShape(string applicationId, List<Mesh> displayValue)
  {
    this.applicationId = applicationId;
    this.displayValue = displayValue;
  }

  // Element base
  public string elementType { get; set; }

  public List<Classification> classifications { get; set; }
  public Base? elementProperties { get; set; }
  public Base? componentProperties { get; set; }

  public ArchicadLevel level { get; set; }

  public string units { get; set; }

  [DetachProperty]
  public List<Mesh> displayValue { get; set; }
}
