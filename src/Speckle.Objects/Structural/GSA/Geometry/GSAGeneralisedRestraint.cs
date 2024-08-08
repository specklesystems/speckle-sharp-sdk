using Speckle.Objects.Structural.Geometry;
using Speckle.Objects.Structural.GSA.Analysis;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Structural.GSA.Geometry;

[SpeckleType("Objects.Structural.GSA.Geometry.GSAGeneralisedRestraint")]
public class GSAGeneralisedRestraint : Base
{
  public GSAGeneralisedRestraint() { }

  [SchemaInfo(
    "GSAGeneralisedRestraint",
    "Creates a Speckle structural generalised restraint (a set of restraint conditions to be applied to a list of nodes) for GSA",
    "GSA",
    "Geometry"
  )]
  public GSAGeneralisedRestraint(
    int nativeId,
    string name,
    Restraint restraint,
    List<Node> nodes,
    List<GSAStage> stages
  )
  {
    this.nativeId = nativeId;
    this.name = name;
    this.restraint = restraint;
    this.nodes = nodes;
    this.stages = stages;
  }

  public int nativeId { get; set; }
  public string name { get; set; }

  [DetachProperty]
  public Restraint restraint { get; set; }

  [DetachProperty, Chunkable(5000)]
  public List<Node> nodes { get; set; }

  [DetachProperty]
  public List<GSAStage> stages { get; set; }
}
