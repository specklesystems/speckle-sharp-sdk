using Speckle.Objects.Primitive;
using Speckle.Sdk.Common;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Geometry;

public enum SpiralType
{
  Biquadratic,
  BiquadraticParabola,
  Bloss,
  Clothoid,
  Cosine,
  Cubic,
  CubicParabola,
  Radioid,
  Sinusoid,
  Unknown,
}

[SpeckleType("Objects.Geometry.Spiral")]
public class Spiral : Base, ICurve, IHasBoundingBox, IDisplayValue<Polyline>
{
  public required Point startPoint { get; set; }
  public required Point endPoint { get; set; }
  public required Plane plane { get; set; } // plane with origin at spiral center
  public required double turns { get; set; } // total angle of spiral. positive is counterclockwise, negative is clockwise
  public required Vector pitchAxis { get; set; } = new(0, 0, 0, Units.None);
  public required double pitch { get; set; }
  public required SpiralType spiralType { get; set; }

  public required string units { get; set; }

  public required double length { get; set; }

  public required Interval domain { get; set; }

  [DetachProperty]
  public required Polyline displayValue { get; set; }

  public Box? bbox { get; set; }
}
