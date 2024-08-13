using Speckle.Newtonsoft.Json;
using Speckle.Objects.Geometry;
using Speckle.Sdk.Models;

namespace Speckle.Objects.GIS;

[SpeckleType("Objects.GIS.GisPolylineFeature")]
public class GisPolylineFeature : Base, IGisFeature, IDisplayValue<List<Polyline>>
{
  public GisPolylineFeature() { }

  public GisPolylineFeature(List<Polyline> lines, Base attributes)
  {
    displayValue = lines;
    this.attributes = attributes;
  }

  public Base attributes { get; set; }

  [JsonIgnore]
  public List<Polyline> geometry
  {
    get { return displayValue; }
    set { displayValue = value; }
  }

  [DetachProperty]
  public List<Polyline> displayValue { get; set; }
}
