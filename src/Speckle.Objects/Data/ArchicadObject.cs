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

  [DetachProperty]
  public required List<ArchicadObject> elements { get; set; }

  IReadOnlyList<IArchicadObject> IArchicadObject.elements => elements;
}
