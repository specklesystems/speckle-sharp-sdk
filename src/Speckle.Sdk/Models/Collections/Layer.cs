namespace Speckle.Sdk.Models.Collections;

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
  public Layer(string name)
  {
    this.name = name;
  }
}
