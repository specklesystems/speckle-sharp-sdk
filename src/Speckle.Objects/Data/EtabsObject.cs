using Speckle.Sdk.Models;

namespace Speckle.Objects.Data;

/// <summary>
/// Represents a model object in ETABS
/// </summary>
[SpeckleType("Objects.Data.EtabsObject")]
public class EtabsObject : Base, ICsiObject
{
  public required string name { get; set; }
  public required string type { get; set; }

  /// <summary>
  /// Children objects, eg joints, this etabs object may contain.
  /// </summary>
  [DetachProperty]
  public required List<EtabsObject> elements { get; set; }

  public required List<Base> displayValue { get; set; }

  public required Dictionary<string, object?> properties { get; set; }

  public required string units { get; set; }

  IReadOnlyList<ICsiObject> ICsiObject.elements => elements;

  IReadOnlyList<Base> IDataObject.displayValue => displayValue;
}
