using System.Diagnostics.CodeAnalysis;
using Speckle.Objects.Other;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Geometry;

[SpeckleType("Objects.Geometry.ControlPoint")]
public class ControlPoint : Point, ITransformable<ControlPoint>
{
  public ControlPoint() { }

  [SetsRequiredMembers]
  public ControlPoint(double x, double y, double z, double w, string units, string? applicationId = null)
    : base(x, y, z, units, applicationId)
  {
    weight = w;
  }

  public required double weight { get; set; }

  public bool TransformTo(Transform transform, out ControlPoint transformed)
  {
    TransformTo(transform, out Point transformedPoint);
    transformed = new ControlPoint(
      transformedPoint.x,
      transformedPoint.y,
      transformedPoint.z,
      weight,
      units,
      applicationId
    );
    return true;
  }

  public override string ToString()
  {
    return $"{{{x},{y},{z},{weight}}}";
  }

  public void Deconstruct(out double x, out double y, out double z, out double weight)
  {
    Deconstruct(out x, out y, out z, out weight, out _);
  }

  public void Deconstruct(out double x, out double y, out double z, out double weight, out string? units)
  {
    Deconstruct(out x, out y, out z, out units);
    weight = this.weight;
  }
}
