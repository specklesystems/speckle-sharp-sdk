using Speckle.Objects.Structural.Geometry;
using Speckle.Objects.Structural.Loading;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Structural.GSA.Bridge;

[SpeckleType("Objects.Structural.GSA.Bridge.GSAInfluenceNode")]
public class GSAInfluenceNode : GSAInfluence
{
  public GSAInfluenceNode() { }

  [SchemaInfo(
    "GSAInfluenceBeam",
    "Creates a Speckle structural node influence effect for GSA (for an influence analysis)",
    "GSA",
    "Bridge"
  )]
  public GSAInfluenceNode(
    int nativeId,
    string name,
    double factor,
    InfluenceType type,
    LoadDirection direction,
    Node node,
    Axis axis
  )
  {
    this.nativeId = nativeId;
    this.name = name;
    this.factor = factor;
    this.type = type;
    this.direction = direction;
    this.node = node;
    this.axis = axis;
  }

  [DetachProperty]
  public Node node { get; set; }

  [DetachProperty]
  public Axis axis { get; set; }
}
