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
  /// Plain text, without formatting
  /// </summary>
  public required string value { get; set; }
  public required Point origin { get; set; }

  /// <summary>
  /// Height in linear units or pixels (if Units.None)
  /// </summary>
  public required double height { get; set; }

  /// <summary>
  /// Units will be 'Units.None' if the text size is defined in pixels (stays the same size
  /// independently of zooming the model). Default height in pixels is 17px (used for Viewer measurements)
  /// </summary>
  public required string units { get; set; }

  /// <summary>
  /// Horizontal alignment: Left, Center or Right
  /// </summary>
  public AlignmentHorizontal alignmentH { get; set; }

  /// <summary>
  /// Vertical alignment: Top, Center or Bottom
  /// </summary>
  public AlignmentVertical alignmentV { get; set; }

  /// <summary>
  /// Plane will be null if the text object orientation follows camera view
  /// </summary>
  public Plane? plane { get; set; }

  /// <summary>
  /// Maximum width of the text field (in 'units').
  /// Text will be split into lines (wrapped) to fit into the width.
  /// null, if text should not be wrapped.
  /// </summary>
  public double? maxWidth { get; set; }
}

public enum AlignmentHorizontal
{
  Left,
  Center,
  Right,
}

public enum AlignmentVertical
{
  Top,
  Center,
  Bottom,
}
