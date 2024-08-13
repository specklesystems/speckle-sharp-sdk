using Speckle.Newtonsoft.Json;
using Speckle.Objects.Geometry;
using Speckle.Sdk.Models;

namespace Speckle.Objects.GIS;

[SpeckleType("Objects.GIS.GisPointFeature")]
public class GisPointFeature : Base, IGisFeature, IDisplayValue<List<Point>>
{
  public GisPointFeature() { }

  public GisPointFeature(List<Point> points, Base attributes)
  {
    displayValue = points;
    this.attributes = attributes;
  }

  public Base attributes { get; set; }

  [JsonIgnore]
  public List<Point> geometry
  {
    get { return displayValue; }
    set { displayValue = value; }
  }

  [DetachProperty]
  public List<Point> displayValue { get; set; }
}
