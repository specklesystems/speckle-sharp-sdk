using Speckle.Objects.Geometry;
using Speckle.Objects.Structural.Geometry;
using Speckle.Objects.Structural.Properties;
using Speckle.Sdk.Common;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Structural.GSA.Geometry;

[SpeckleType("Objects.Structural.GSA.Geometry.GSANode")]
public class GSANode : Node
{
  public GSANode() { }

  /// <summary>
  /// SchemaBuilder constructor for a GSA node
  /// </summary>
  /// <param name="basePoint"></param>
  /// <param name="restraint"></param>
  /// <param name="constraintAxis"></param>
  /// <param name="springProperty"></param>
  /// <param name="massProperty"></param>
  /// <param name="damperProperty"></param>
  /// <param name="localElementSize"></param>
  [SchemaInfo("GSANode", "Creates a Speckle structural node for GSA", "GSA", "Geometry")]
  public GSANode(
    int nativeId,
    Point basePoint,
    Restraint restraint,
    Axis? constraintAxis = null,
    PropertySpring? springProperty = null,
    PropertyMass? massProperty = null,
    PropertyDamper? damperProperty = null,
    double localElementSize = 0,
    string colour = "NO_RGB"
  )
  {
    this.nativeId = nativeId;
    this.basePoint = basePoint;
    this.restraint = restraint;
    this.constraintAxis =
      constraintAxis == null
        ? new Axis(
          "Global",
          AxisType.Cartesian,
          new Plane()
          {
            origin = new Point(0, 0, 0, units: Units.Meters), //Defaulting to meters here was probably not intentional, but previously was being done in the Vector's ctor
            normal = new Vector(0, 0, 1, units: Units.Meters), //Defaulting to meters here was probably not intentional, but previously was being done in the Vector's ctor
            xdir = new Vector(1, 0, 0, units: Units.Meters), //Defaulting to meters here was probably not intentional, but previously was being done in the Vector's ctor
            ydir = new Vector(0, 1, 0, units: Units.Meters), //Defaulting to meters here was probably not intentional, but previously was being done in the Vector's ctor
            units =
              Units.Meters //Not sure if defaulting to meters is correct, but it was what we were doing previously inside Plane's ctor
            ,
          }
        )
        : constraintAxis;
    this.springProperty = springProperty;
    this.massProperty = massProperty;
    this.damperProperty = damperProperty;
    this.localElementSize = localElementSize;
    this.colour = colour;
  }

  public int nativeId { get; set; }
  public double localElementSize { get; set; }
  public string colour { get; set; }
}
