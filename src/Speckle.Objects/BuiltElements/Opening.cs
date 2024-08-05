using Speckle.Sdk.Host;
using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements;

[SpeckleType("Objects.BuiltElements.Opening")]
public class Opening : Base
{
  public Opening() { }

  [SchemaInfo("Arch Opening", "Creates a Speckle opening", "BIM", "Architecture")]
  public Opening(ICurve outline)
  {
    this.outline = outline;
  }

  public ICurve outline { get; set; }

  public string units { get; set; }
}
