using Speckle.Sdk.Models;

namespace Speckle.Objects.Data;

[SpeckleType("Objects.Data.TsdObject")]
public class TsdObject : DataObject, ITsdObject
{
  public required string type { get; set; }

  [DetachProperty]
  public required List<TsdObject> elements { get; set; }

  public required string units { get; set; }

  IReadOnlyList<ITsdObject> ITsdObject.elements => elements;

  IReadOnlyList<Base> IDisplayValue<IReadOnlyList<Base>>.displayValue => displayValue;
}
