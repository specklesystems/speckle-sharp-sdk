using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Primitive;

[SpeckleType("Objects.Primitive.Interval")]
public class Interval : Base
{
  public required double start { get; set; }
  public required double end { get; set; }

  [JsonIgnore]
  public double Length => Math.Abs((end) - (start));

  public override string ToString()
  {
    return base.ToString() + $"[{start}, {end}]";
  }

  public static Interval UnitInterval => new() { start = 0, end = 1 };
}
