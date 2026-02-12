using System.Diagnostics.CodeAnalysis;
using Speckle.Newtonsoft.Json;
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

  /// <summary>
  /// OBSOLETE - This is just here for backwards compatibility.
  /// </summary>
  [
    JsonProperty(NullValueHandling = NullValueHandling.Ignore),
    Obsolete("Access coordinates using XYZ and weight fields", true)
  ]
  internal new List<double> value
  {
#pragma warning disable CS8603 // Possible null reference return. Reason: obsolete.
    get => null;
#pragma warning restore CS8603 // Possible null reference return. Reason: obsolete.
    set
    {
      x = value[0];
      y = value[1];
      z = value[2];
      weight = value.Count > 3 ? value[3] : 1;
    }
  }

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
