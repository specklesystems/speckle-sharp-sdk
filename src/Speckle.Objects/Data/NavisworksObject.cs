using Speckle.Sdk.Models;

namespace Speckle.Objects.Data;

/// <summary>
/// Represents a "first selectable ancestor" Navisworks.ModelItem object in Navisworks
/// </summary>
[SpeckleType("Objects.Data.NavisworksObject")]
public class NavisworksObject : Base, INavisworksObject
{
  public required string name { get; set; }

  public required List<Base> displayValue { get; set; }

  public required Dictionary<string, object?> properties { get; set; }

  public required string units { get; set; }

  IReadOnlyList<Base> IDataObject.displayValue => displayValue;
}
