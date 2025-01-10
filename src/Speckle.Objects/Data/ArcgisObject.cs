using Speckle.Sdk.Models;

namespace Speckle.Objects.Data;

/// <summary>
/// Represents a ArcGIS.Core.CoreObjectsBase object in ArcGIS
/// </summary>
[SpeckleType("Objects.Data.ArcgisObject")]
public class ArcgisObject : DataObject, IGisObject
{
  public required string type { get; set; }

  public required string units { get; set; }

  IReadOnlyList<Base> IDisplayValue<IReadOnlyList<Base>>.displayValue => displayValue;
}
