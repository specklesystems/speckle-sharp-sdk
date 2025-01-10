using Speckle.Newtonsoft.Json;
using Speckle.Objects.Geometry;
using Speckle.Sdk.Models;

namespace Speckle.Objects.GIS;

[SpeckleType("Objects.GIS.GisPointFeature")]
public class GisPointFeature : Base, IGisFeature, IDisplayValue<List<Point>>
{
  public required Base attributes { get; set; }

  [JsonIgnore]
  public required List<Point> geometry { get; set; }

  [DetachProperty]
  public List<Point> displayValue
  {
    get => geometry;
    set => geometry = value;
  }
}
