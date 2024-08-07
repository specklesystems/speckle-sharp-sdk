using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Primitive;

[SpeckleType("Objects.Primitive.Interval")]
public class Interval : Base
{
  public Interval() { }

  public Interval(double start, double end)
  {
    this.start = start;
    this.end = end;
  }

  public double? start { get; set; }
  public double? end { get; set; }

  [JsonIgnore]
  public double Length => Math.Abs((end ?? 0) - (start ?? 0));

  public override string ToString()
  {
    return base.ToString() + $"[{start}, {end}]";
  }
}
