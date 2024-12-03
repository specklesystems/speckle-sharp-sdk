using Speckle.Sdk.Models;

namespace Speckle.Objects.Data;

/// <summary>
/// Represents an Autodesk.Civil.DatabaseServices.Entity object in Civil3d
/// </summary>
[SpeckleType("Objects.Data.Civil3dObject")]
public class Civil3dObject : Base, ICivilObject
{
  public required string name { get; set; }
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

  public required List<Base> displayValue { get; set; }

  public required Dictionary<string, object?> properties { get; set; }

  public required string units { get; set; }

  IReadOnlyList<Base> ICivilObject.elements => elements;

  IReadOnlyList<Base> IDataObject.displayValue => displayValue;
}
