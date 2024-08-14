using Speckle.Objects.Geometry;
using Speckle.Sdk.Models;

namespace Speckle.Objects.GIS;

[SpeckleType("Objects.GIS.GisPolygonFeature")]
public class GisPolygonFeature : Base, IGisFeature, IDisplayValue<List<Mesh>>
{
  public required Base attributes { get; set; }

  [DetachProperty]
  public required List<PolygonGeometry> geometry { get; set; }

  [DetachProperty]
  public required List<Mesh> displayValue { get; set; }
}
