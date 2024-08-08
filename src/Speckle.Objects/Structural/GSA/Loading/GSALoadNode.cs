using Speckle.Objects.Structural.Geometry;
using Speckle.Objects.Structural.GSA.Geometry;
using Speckle.Objects.Structural.Loading;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Structural.GSA.Loading;

[SpeckleType("Objects.Structural.GSA.Loading.GSALoadNode")]
public class GSALoadNode : LoadNode
{
  public GSALoadNode() { }

  [SchemaInfo("GSALoadNode", "Creates a Speckle node load for GSA", "GSA", "Loading")]
  public GSALoadNode(
    int nativeId,
    string name,
    LoadCase loadCase,
    List<GSANode> nodes,
    LoadDirection direction,
    double value
  )
  {
    this.nativeId = nativeId;
    this.name = name;
    this.loadCase = loadCase;
    List<Node> baseNodes = nodes.ConvertAll(x => (Node)x);
    this.nodes = baseNodes;
    this.direction = direction;
    this.value = value;
  }

  [SchemaInfo(
    "GSALoadNode (user-defined axis)",
    "Creates a Speckle node load (user-defined axis) for GSA",
    "GSA",
    "Loading"
  )]
  public GSALoadNode(
    int nativeId,
    string name,
    LoadCase loadCase,
    List<Node> nodes,
    Axis loadAxis,
    LoadDirection direction,
    double value
  )
  {
    this.nativeId = nativeId;
    this.name = name;
    this.loadCase = loadCase;
    this.nodes = nodes;
    this.loadAxis = loadAxis;
    this.direction = direction;
    this.value = value;
  }

  public int nativeId { get; set; }
}
