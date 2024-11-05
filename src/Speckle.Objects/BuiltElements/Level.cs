using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements;

[SpeckleType("Objects.BuiltElements.Level")]
public class Level : Base
{
  public required string name { get; set; }
  public required double elevation { get; set; }

  public string units { get; set; }
}
