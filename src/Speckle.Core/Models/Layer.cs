namespace Speckle.Core.Models;

/// <summary>
/// A specialized collection that represents a CAD-app layer. We expect this to grow in the future with possibly other shared props.
/// </summary>
public class Layer : Collection
{
  public Layer() { }

  /// <summary>
  /// Constructor for a basic Layer.
  /// </summary>
  /// <param name="name">The human-readable name of this collection</param>
  /// <param name="color"></param>
  public Layer(string name, int color)
  {
    this.name = name;
    this.color = color;
  }

  /// <summary>
  /// The argb int value of the layer color
  /// </summary>
  public int color { get; set; }
}
