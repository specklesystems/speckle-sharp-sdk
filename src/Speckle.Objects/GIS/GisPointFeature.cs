using Speckle.Objects.Geometry;
using Speckle.Sdk.Models;

namespace Speckle.Objects.GIS;

[SpeckleType("Objects.GIS.GisPointFeature")]
public class GisPointFeature : Base, IGisFeature, IDisplayValue<List<Point>>
{
  public required Base attributes { get; set; }

  [Newtonsoft.Json.JsonIgnore]
  [System.Text.Json.Serialization.JsonIgnore]
  public required List<Point> geometry
  {
    get => displayValue;
    set => displayValue = value;
  }

  [DetachProperty]
  public List<Point> displayValue { get; set; }
}
