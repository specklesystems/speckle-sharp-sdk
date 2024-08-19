using Speckle.Objects.Geometry;
using Speckle.Objects.Structural.CSI.Properties;
using Speckle.Objects.Structural.Geometry;
using Speckle.Objects.Structural.Properties;
using Speckle.Objects.Structural.Results;
using Speckle.Sdk.Common;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Structural.CSI.Geometry;

[SpeckleType("Objects.Structural.CSI.Geometry.CSINode")]
public class CSINode : Node
{
  [SchemaInfo(
    "Node with properties",
    "Creates a Speckle CSI node with spring, mass and/or damper properties",
    "CSI",
    "Geometry"
  )]
  public CSINode(
    Point basePoint,
    string? name = null,
    [SchemaParamInfo("If null, restraint condition defaults to free/fully released")] Restraint? restraint = null,
    [SchemaParamInfo(
      "If null, axis defaults to world xy (z axis defines the vertical direction, positive direction is up)"
    )]
      Axis? constraintAxis = null,
    CSISpringProperty? springProperty = null,
    PropertyMass? massProperty = null,
    PropertyDamper? damperProperty = null,
    CSIDiaphragm? CSIDiaphragm = null,
    DiaphragmOption DiaphragmOption = DiaphragmOption.FromShellObject
  )
  {
    this.basePoint = basePoint;
    this.name = name;
    this.restraint = restraint ?? new Restraint("RRRRRR");
    this.constraintAxis =
      constraintAxis
      ?? new Axis(
        "Global",
        AxisType.Cartesian,
        new Plane
        {
          origin = new Point(0, 0),
          normal = new Vector(0, 0, 1),
          xdir = new Vector(1, 0, 0),
          ydir = new Vector(0, 1, 0),
          units = Units.Meters, //Not sure if defaulting to meters is correct, but it was what we were doing previously inside Plane's ctor
        }
      );
    CSISpringProperty = springProperty;
    this.massProperty = massProperty;
    this.damperProperty = damperProperty;
    DiaphragmAssignment = CSIDiaphragm?.name;
    this.DiaphragmOption = DiaphragmOption;
  }

  public CSINode() { }

  [DetachProperty]
  public CSISpringProperty? CSISpringProperty { get; set; }

  public string? DiaphragmAssignment { get; set; }

  public DiaphragmOption DiaphragmOption { get; set; }

  [DetachProperty]
  public AnalyticalResults? AnalysisResults { get; set; }
}
