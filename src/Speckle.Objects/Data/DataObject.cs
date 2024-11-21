using Speckle.Sdk.Models;

namespace Speckle.Objects.Data;

[SpeckleType("Objects.Data.DataObject")]
public class DataObject : Base, IDisplayValue<List<Base>>
{
  public required string name { get; set; }

  public required List<Base> displayValue { get; set; }
}
