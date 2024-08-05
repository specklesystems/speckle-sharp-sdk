using Speckle.Objects.Geometry;
using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements.TeklaStructures;

[SpeckleType("Objects.BuiltElements.TeklaStructures.Fitting")]
public class Fitting : Plane
{
  public string hostID { get; set; }
}
