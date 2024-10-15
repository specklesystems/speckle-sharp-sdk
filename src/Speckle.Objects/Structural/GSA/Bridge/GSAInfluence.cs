using Speckle.Objects.Structural.Loading;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Structural.GSA.Bridge;

[SpeckleType("Objects.Structural.GSA.Bridge.GSAInfluence")]
public class GSAInfluence : Base
{
  public GSAInfluence() { }

  public GSAInfluence(int nativeId, string name, double factor, InfluenceType type, LoadDirection direction)
  {
    this.nativeId = nativeId;
    this.name = name;
    this.factor = factor;
    this.type = type;
    this.direction = direction;
  }

  public int nativeId { get; set; }
  public string name { get; set; }
  public double factor { get; set; }
  public InfluenceType type { get; set; }
  public LoadDirection direction { get; set; }
}

public enum InfluenceType
{
  NotSet = 0,
  FORCE,
  DISPLACEMENT,
}
