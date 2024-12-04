using Speckle.Sdk.Models;

namespace Speckle.Objects.Data;

[SpeckleType("Objects.Data.DataObject")]
public class DataObject : Base, IDataObject
{
  public required string name { get; set; }

  [DetachProperty]
  public required List<Base> displayValue { get; set; }

  public required Dictionary<string, object?> properties { get; set; }

  IReadOnlyList<Base> IDisplayValue<IReadOnlyList<Base>>.displayValue => displayValue;
}
