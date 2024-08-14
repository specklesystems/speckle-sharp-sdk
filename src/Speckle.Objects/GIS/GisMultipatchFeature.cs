using Speckle.Objects.Geometry;
using Speckle.Sdk.Models;

namespace Speckle.Objects.GIS;

[SpeckleType("Objects.GIS.GisMultipatchFeature")]
public class GisMultipatchFeature : Base, IGisFeature, IDisplayValue<List<Mesh>>
{
  public required Base attributes { get; set; }

  /// <summary>
  /// Multipatch geometry should be of type <see cref="GisMultipatchGeometry"/> or <see cref="PolygonGeometry3d"/>
  /// </summary>
  [DetachProperty]
  public required List<Base> geometry { get; set; }

  [DetachProperty]
  public required List<Mesh> displayValue { get; set; }
}
