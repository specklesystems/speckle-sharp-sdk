using Speckle.Sdk.Models;

namespace Speckle.Objects.Other;

[SpeckleType("Objects.Other.Hatch")]
public class Hatch : Base
{
  [Obsolete("Use Loops instead")]
  public List<ICurve> curves { get; set; }

  public List<HatchLoop> loops { get; set; }
  public string pattern { get; set; }
  public double scale { get; set; } = 1;
  public double rotation { get; set; } // relative angle
}

/// <summary>
/// Represents a Hatch Loop from a <see cref="Hatch"/>'s curve.
/// </summary>
[SpeckleType("Objects.Other.HatchLoop")]
public class HatchLoop : Base
{
  public HatchLoop() { }

  public HatchLoop(ICurve curve, HatchLoopType type)
  {
    Curve = curve;
    Type = type;
  }

  public ICurve Curve { get; set; }
  public HatchLoopType Type { get; set; }
}

/// <summary>
/// Represents the type of a loop in a <see cref="Hatch"/>'s curves.
/// </summary>
public enum HatchLoopType
{
  Unknown,
  Outer,
  Inner
}
