using Speckle.Objects.Geometry;
using Speckle.Objects.Structural.GSA.Geometry;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Structural.GSA.Loading;

public class GSAPolyline : Polyline
{
  public GSAPolyline() { }

  [SchemaInfo("GSAPolyline", "Creates a Speckle structural polyline for GSA", "GSA", "Geometry")]
  public GSAPolyline(
    string name,
    int nativeId,
    IEnumerable<double> coordinatesArray,
    string colour,
    GSAGridPlane gridPlane
  )
  {
    this.name = name;
    this.nativeId = nativeId;
    value = coordinatesArray.ToList();
    this.colour = colour;
    this.gridPlane = gridPlane;
  }

  public string name { get; set; }
  public int nativeId { get; set; }
  public string colour { get; set; }

  [DetachProperty]
  public GSAGridPlane gridPlane { get; set; }
}
