using Speckle.Sdk.Models;

namespace Speckle.Objects.Data;

/// <summary>
/// Represents a ArcGIS.Core.CoreObjectsBase object in ArcGIS
/// </summary>
[SpeckleType("Objects.Data.ArcgisObject")]
[Obsolete("ArcGIS is not supported")] //NOTE: when we remove this class, we should add it its speckle_type to v2 legacy (or similar) to maintain backwards compat on receive
public class ArcgisObject : DataObject, IGisObject
{
  public required string type { get; set; }

  public required string units { get; set; }

  IReadOnlyList<Base> IDisplayValue<IReadOnlyList<Base>>.displayValue => displayValue;
}
