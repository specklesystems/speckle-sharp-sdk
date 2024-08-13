using Speckle.Objects.Geometry;
using Speckle.Sdk.Models;

namespace Speckle.Objects.GIS;

[SpeckleType("Objects.GIS.GisPolygonFeature")]
public class GisPolygonFeature : Base, IGisFeature, IDisplayValue<List<Mesh>>
{
  public GisPolygonFeature() { }

  public GisPolygonFeature(List<PolygonGeometry> polygons, List<Mesh> meshes, Base attributes)
  {
    geometry = polygons;
    displayValue = meshes;
    this.attributes = attributes;
  }

  public Base attributes { get; set; }

  [DetachProperty]
  public List<PolygonGeometry> geometry { get; set; }

  [DetachProperty]
  public List<Mesh> displayValue { get; set; }
}
