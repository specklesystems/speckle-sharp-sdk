using Speckle.Sdk.Models;

namespace Speckle.Objects.Data;

/// <summary>
/// Represents a wrapper object in ETABS
/// </summary>
[SpeckleType("Objects.Data.EtabsObject")]
public class EtabsObject : DataObject, ICsiObject
{
  public required string type { get; set; }

  /// <summary>
  /// Children objects, eg joints, this etabs object may contain.
  /// </summary>
  [DetachProperty]
  public required List<EtabsObject> elements { get; set; }

  public required string units { get; set; }

  IReadOnlyList<ICsiObject> ICsiObject.elements => elements;

  IReadOnlyList<Base> IDisplayValue<IReadOnlyList<Base>>.displayValue => displayValue;
}
