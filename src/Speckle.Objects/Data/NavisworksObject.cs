using Speckle.Sdk.Models;

namespace Speckle.Objects.Data;

/// <summary>
/// Represents a "first selectable ancestor" Navisworks.ModelItem object in Navisworks
/// </summary>
[SpeckleType("Objects.Data.NavisworksObject")]
public class NavisworksObject : DataObject, INavisworksObject
{
  public required string units { get; set; }

  IReadOnlyList<Base> IDisplayValue<IReadOnlyList<Base>>.displayValue => displayValue;
}
