using Speckle.Sdk.Models;

namespace Speckle.Objects.GIS;

[Obsolete("NonGeometryElement was replaced by a more generic class, \"GisFeature\", which contains more information")]
[SpeckleType("Objects.GIS.NonGeometryElement")]
public class NonGeometryElement : Base
{
  public Base? attributes { get; set; }
}
