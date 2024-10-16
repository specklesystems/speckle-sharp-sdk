using Speckle.Objects.Geometry;
using Speckle.Objects.Structural.Loading;
using Speckle.Sdk.Common;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Structural.GSA.Loading;

[SpeckleType("Objects.Structural.GSA.Loading.GSALoadGravity")]
public class GSALoadGravity : LoadGravity
{
  public GSALoadGravity() { }

  [SchemaInfo(
    "GSALoadGravity",
    "Creates a Speckle structural gravity load (applied to all nodes and elements) for GSA",
    "GSA",
    "Loading"
  )]
  public GSALoadGravity(int nativeId, string name, LoadCase loadCase, Vector? gravityFactors = null)
  {
    this.nativeId = nativeId;
    this.name = name;
    this.loadCase = loadCase;
    this.gravityFactors = gravityFactors ?? new Vector(0, 0, -1, Units.Meters); //Defaulting to meters here was probably not intentional, but previously was being done in the Vector's ctor
  }

  [SchemaInfo(
    "GSALoadGravity (specified elements)",
    "Creates a Speckle structural gravity load (applied to specified elements) for GSA",
    "GSA",
    "Loading"
  )]
  public GSALoadGravity(
    int nativeId,
    string name,
    LoadCase loadCase,
    List<Base> elements,
    Vector? gravityFactors = null
  )
  {
    this.nativeId = nativeId;
    this.name = name;
    this.elements = elements;
    this.loadCase = loadCase;
    this.gravityFactors = gravityFactors ?? new Vector(0, 0, -1, Units.Meters); //Defaulting to meters here was probably not intentional, but previously was being done in the Vector's ctor
  }

  [SchemaInfo(
    "GSALoadGravity (specified elements and nodes)",
    "Creates a Speckle structural gravity load (applied to specified nodes and elements) for GSA",
    "GSA",
    "Loading"
  )]
  public GSALoadGravity(
    int nativeId,
    string name,
    LoadCase loadCase,
    List<Base> elements,
    List<Base> nodes,
    Vector? gravityFactors = null,
    string? nativedId = null
  )
  {
    this.nativeId = nativeId;
    this.name = name;
    this.elements = elements;
    this.nodes = nodes;
    this.loadCase = loadCase;
    this.gravityFactors = gravityFactors ?? new Vector(0, 0, -1, Units.Meters); //Defaulting to meters here was probably not intentional, but previously was being done in the Vector's ctor
  }

  public int nativeId { get; set; }
}
