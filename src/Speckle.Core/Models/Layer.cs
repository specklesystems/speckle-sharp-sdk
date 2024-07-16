namespace Speckle.Core.Models;

/// <summary>
/// A specialized collection that represents a CAD-app layer. We expect this to grow in the future with possibly other shared props.
/// </summary>
public class Layer : Collection
{
  public int Color { get; set; }
}
