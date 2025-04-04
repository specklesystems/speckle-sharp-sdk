using System.Drawing;
using Speckle.Objects.Geometry;
using Speckle.Sdk.Models;
using Point = Speckle.Objects.Geometry.Point;

namespace Speckle.Objects.Other;

/// <summary>
/// Text class for representation in the viewer
/// </summary>
[SpeckleType("Objects.Other.Text")]
public class Text : Base
{
  public required string value { get; set; }
  public required Point origin { get; set; }
  public required double? height { get; set; }
  public required string? units { get; set; }
  public int justificationH { get; set; }
  public int justificationV { get; set; }
  public int color { get; set; } = Color.Black.ToArgb();
  public Plane? plane { get; set; }
}
