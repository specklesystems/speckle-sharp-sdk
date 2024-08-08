using Speckle.Sdk.Host;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Other;

/// <summary>
/// Generic class for materials containing generic parameters
/// </summary>
[SpeckleType("Objects.Other.Material")]
public class Material : Base
{
  public Material() { }

  [SchemaInfo("RevitMaterial", "Creates a Speckle material", "BIM", "Architecture")]
  public Material(string name)
  {
    this.name = name;
  }

  public string name { get; set; }
}
