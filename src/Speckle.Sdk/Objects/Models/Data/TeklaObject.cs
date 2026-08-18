using Speckle.Sdk.Models;

namespace Speckle.Objects.Data;

/// <summary>
/// Represents an Tekla.Structures.Model.ModelObject object in Tekla Structures
/// </summary>
[SpeckleType("Objects.Data.TeklaObject")]
public class TeklaObject : DataObject, ITeklaObject
{
  public required string type { get; set; }

  /// <summary>
  /// Children objects, eg profiles, this tekla modelobject may contain.
  /// </summary>
  [DetachProperty]
  public required List<TeklaObject> elements { get; set; }

  public required string units { get; set; }

  IReadOnlyList<ITeklaObject> ITeklaObject.elements => elements;

  IReadOnlyList<Base> IDisplayValue<IReadOnlyList<Base>>.displayValue => displayValue;
}
