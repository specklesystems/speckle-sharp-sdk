using Speckle.Objects.Geometry;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Data;

[SpeckleType("Objects.Data.RevitObject")]
public class RevitObject : Base, IRevitObject
{
  public required string name { get; set; }
  public required string type { get; set; }
  public required string family { get; set; }
  public required string category { get; set; }

  /// <summary>
  /// A Curve or Point object representing the location of a Revit element.
  /// </summary>
  public required Base? location { get; set; }

  /// <summary>
  /// Children objects, eg hosted elements, this RevitObject may contain.
  /// </summary>
  [DetachProperty]
  public required List<RevitObject> elements { get; set; }

  public required List<Mesh> displayValue { get; set; }

  public required string units { get; set; }

  IReadOnlyList<IRevitObject> IRevitObject.elements => elements;

  IReadOnlyList<Base> IDataObject.displayValue => displayValue;
}
