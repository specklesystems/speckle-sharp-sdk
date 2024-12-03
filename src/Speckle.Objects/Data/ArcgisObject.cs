using Speckle.Sdk.Models;

namespace Speckle.Objects.Data;

/// <summary>
/// Represents a ArcGIS.Core.CoreObjectsBase object in ArcGIS
/// </summary>
[SpeckleType("Objects.Data.ArcgisObject")]
public class ArcgisObject : Base, IGisObject
{
  public required string name { get; set; }
  public required string type { get; set; }

  public required List<Base>? displayValue { get; set; }

  public required Dictionary<string, object?> properties { get; set; }

  public required string units { get; set; }

  IReadOnlyList<Base>? IDataObject.displayValue => displayValue;
}
