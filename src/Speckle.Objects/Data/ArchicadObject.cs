using Speckle.Sdk.Models;

namespace Speckle.Objects.Data;

/// <summary>
/// Represents a base class object in Archicad
/// </summary>
[SpeckleType("Objects.Data.ArchicadObject")]
public class ArchicadObject : DataObject, IArchicadObject
{
  public required string type { get; set; }

  public required string level { get; set; }

  /// <summary>
  /// The full classification tree path of an Archicad object
  /// </summary>
  /// <remarks>
  /// The list order is from root to leaf of the classification tree
  /// </remarks>
  public required List<string> classification { get; set; }
}
