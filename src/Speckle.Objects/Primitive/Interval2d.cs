using System.Diagnostics.CodeAnalysis;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Primitive;

[SpeckleType("Objects.Primitive.Interval2d")]
public class Interval2d : Base
{
  public Interval2d() { }

  [SetsRequiredMembers]
  public Interval2d(double start_u, double end_u, double start_v, double end_v)
  {
    u = new Interval { start = start_u, end = end_u };
    v = new Interval { start = start_v, end = end_v };
  }

  public required Interval u { get; set; }
  public required Interval v { get; set; }
}
