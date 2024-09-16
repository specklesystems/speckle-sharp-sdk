using Speckle.Objects.Geometry;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Other;

/// <summary>
/// Text class for Rhino and AutoCAD
/// </summary>
[SpeckleType("Objects.Other.Text")]
public class Text : Base, IDisplayValue<List<Polyline>>
{
  public required Plane plane { get; set; } // origin should be center
  public double rotation { get; set; } // using radians
  public required string value { get; set; } // text without RTF
  public string? richText { get; set; }
  public double height { get; set; }
  public required string units { get; set; }
  public required List<Polyline> displayValue { get; set; }
}
