using Speckle.Sdk.Models;

namespace Speckle.Objects.Data;

[SpeckleType("Objects.Data.CSiObject")]
public class CSiObject : Base, ICSiObject
{
  public required string name { get; set; }
  public required string type { get; set; }

  [DetachProperty]
  public required List<Base> displayValue { get; set; }

  public required string units { get; set; }

  IReadOnlyList<Base> IDataObject.displayValue => displayValue;
}
