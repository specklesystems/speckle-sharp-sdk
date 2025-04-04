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
  /// <summary>
  /// The text itself, without formatting
  /// </summary>
  public required string value { get; set; }
  public required Point origin { get; set; }

  /// <summary>
  /// Height in linear units or pixels (keep "units" as null)
  /// </summary>
  public required double height { get; set; }

  /// <summary>
  /// Units will be null if the text size is defined in pixels (stays the same size
  /// independently of zooming the model). Default pixel size it 17px (used for Viewer measurements)
  /// </summary>
  public required string? units { get; set; }
  public int alignmentH { get; set; }
  public int alignmentV { get; set; }
  public int color { get; set; } = Color.Black.ToArgb();

  /// <summary>
  /// Plane will be null if the text object orientation follows camera view
  /// </summary>
  public Plane? plane { get; set; }
}
