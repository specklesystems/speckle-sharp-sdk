using System.Diagnostics.CodeAnalysis;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Primitive;

[SpeckleType("Objects.Primitive.Interval2d")]
public class Interval2d : Base
{
  //Main empty constructor, `required` keyword enforces setting u and v in initialiser
  public Interval2d() { }

  //Alternate heavy constructor, `SetRequiredMembers` used to disuade users from also setting u and v in initialiser
  [SetsRequiredMembers]
  public Interval2d(double start_u, double end_u, double start_v, double end_v)
  {
    u = new Interval { start = start_u, end = end_u };
    v = new Interval { start = start_v, end = end_v };
  }

  public required Interval u { get; set; }
  public required Interval v { get; set; }
}
