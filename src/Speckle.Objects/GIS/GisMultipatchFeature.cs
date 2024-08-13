using Speckle.Objects.Geometry;
using Speckle.Sdk.Models;

namespace Speckle.Objects.GIS;

[SpeckleType("Objects.GIS.GisPolygonFeature")]
public class GisMultipatchFeature : Base, IGisFeature, IDisplayValue<List<Mesh>>
{
  public GisMultipatchFeature() { }

  public GisMultipatchFeature(List<Base> geometry, List<Mesh> meshes, Base attributes)
  {
    this.geometry = geometry;
    displayValue = meshes;
    this.attributes = attributes;
  }

  public Base attributes { get; set; }

  /// <summary>
  /// Multipatch geometry should be of type <see cref="GisMultipatchGeometry"/> or <see cref="PolygonGeometry3d"/>
  /// </summary>
  [DetachProperty]
  public List<Base> geometry { get; set; }

  [DetachProperty]
  public List<Mesh> displayValue { get; set; }
}
