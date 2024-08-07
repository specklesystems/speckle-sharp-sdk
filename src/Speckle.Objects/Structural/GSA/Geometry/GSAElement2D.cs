using Speckle.Objects.Structural.Geometry;
using Speckle.Objects.Structural.Properties;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Structural.GSA.Geometry;

[SpeckleType("Objects.Structural.GSA.Geometry.GSAElement2D")]
public class GSAElement2D : Element2D
{
  public GSAElement2D() { }

  [SchemaInfo("GSAElement2D", "Creates a Speckle structural 2D element for GSA", "GSA", "Geometry")]
  public GSAElement2D(
    int nativeId,
    List<Node> nodes,
    Property2D property,
    ElementType2D type,
    string? name = null,
    double offset = 0,
    double orientationAngle = 0,
    int group = 0,
    string colour = "NO_RGB",
    bool isDummy = false
  )
  {
    this.nativeId = nativeId;
    topology = nodes;
    this.property = property;
    this.type = type;
    this.name = name ?? "";
    this.nativeId = nativeId;
    this.offset = offset;
    this.orientationAngle = orientationAngle;
    this.group = group;
    this.colour = colour;
    this.isDummy = isDummy;
  }

  public int nativeId { get; set; }
  public int group { get; set; }
  public string colour { get; set; }
  public bool isDummy { get; set; }
}
