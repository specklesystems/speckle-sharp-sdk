using Speckle.Sdk.Host;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Structural.Properties;

public class Property : Base
{
  public Property() { }

  [SchemaInfo("Property", "Creates a Speckle structural property", "Structural", "Properties")]
  public Property(string name)
  {
    this.name = name;
  }

  public string name { get; set; }
}
