using Speckle.Sdk.Models;

namespace Speckle.Objects.Data;

/// <summary>
/// Represents an Autodesk.Revit.DB.Element object in Revit
/// </summary>
[SpeckleType("Objects.Data.RevitObject")]
public class RevitObject : DataObject, IRevitObject
{
  public required string type { get; set; }
  public required string family { get; set; }
  public required string category { get; set; }

  /// <summary>
  /// The level constraint of the object. For objects constrained by multiple levels, this represents the base constraint.
  /// </summary>
  public required string level { get; set; }

  /// <summary>
  /// A Curve or Point object representing the location of a Revit element.
  /// </summary>
  public required Base? location { get; set; }

  /// <summary>
  /// Children objects, eg hosted elements, this RevitObject may contain.
  /// </summary>
  [DetachProperty]
  public required List<RevitObject> elements { get; set; }

  public required string units { get; set; }

  IReadOnlyList<IRevitObject> IRevitObject.elements => elements;
}
