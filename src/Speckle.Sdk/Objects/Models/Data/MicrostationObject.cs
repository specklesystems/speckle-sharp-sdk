using Speckle.Sdk.Models;

namespace Speckle.Objects.Data;

/// <summary>
/// Represents a base class object in Microstation
/// </summary>
[SpeckleType("Objects.Data.MicrostationObject")]
public class MicrostationObject : DataObject, IMicrostationObject
{
  public required string type { get; set; }

  public required string units { get; set; }

  IReadOnlyList<Base> IDisplayValue<IReadOnlyList<Base>>.displayValue => displayValue;
}
