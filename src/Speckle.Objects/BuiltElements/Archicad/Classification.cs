using Speckle.Sdk.Host;
using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements.Archicad;

[SpeckleType("Objects.BuiltElements.Archicad.Classification")]
public class Classification : Base
{
  public Classification() { }

  [SchemaInfo("Classification", "A classification to set on an element", "BIM", "All")]
  public Classification(string system, string? code = null, string? name = null)
  {
    this.system = system;
    this.code = code;
    this.name = name;
  }

  public string system { get; set; }
  public string? code { get; set; }
  public string? name { get; set; }
}
