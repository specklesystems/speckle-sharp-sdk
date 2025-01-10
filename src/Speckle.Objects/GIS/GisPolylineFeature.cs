using Speckle.Newtonsoft.Json;
using Speckle.Objects.Geometry;
using Speckle.Sdk.Models;

namespace Speckle.Objects.GIS;

[SpeckleType("Objects.GIS.GisPolylineFeature")]
public class GisPolylineFeature : Base, IGisFeature, IDisplayValue<List<Polyline>>
{
  public required Base attributes { get; set; }

  [JsonIgnore]
  public required List<Polyline> geometry { get; set; }

  [DetachProperty]
  public List<Polyline> displayValue
  {
    get => geometry;
    set => geometry = value;
  }
}
