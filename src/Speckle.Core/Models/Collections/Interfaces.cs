namespace Speckle.Core.Models.Collections;

/// <summary>
/// Represents a collection that has a <see cref="IHasColor.color"/>
/// </summary>
public interface IHasColor
{
  /// <summary>
  /// The argb int value of the collection color
  /// </summary>
  public int color { get; set; }
}
