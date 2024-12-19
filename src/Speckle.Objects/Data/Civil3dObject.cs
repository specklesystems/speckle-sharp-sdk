using Speckle.Sdk.Models;

namespace Speckle.Objects.Data;

/// <summary>
/// Represents an Autodesk.Civil.DatabaseServices.Entity object in Civil3d
/// </summary>
[SpeckleType("Objects.Data.Civil3dObject")]
public class Civil3dObject : DataObject, ICivilObject
{
  public required string type { get; set; }

  /// <summary>
  /// Curves representing the base curve of an entity
  /// </summary>
  public required List<ICurve>? baseCurves { get; set; }

  /// <summary>
  /// Children objects, eg profiles, this civil entity may contain.
  /// </summary>
  [DetachProperty]
  public required List<Base> elements { get; set; }

  public required string units { get; set; }

  IReadOnlyList<Base> ICivilObject.elements => elements;
}
