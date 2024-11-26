using Speckle.Sdk.Models;

namespace Speckle.Objects.Data;

/// <summary>
/// Represents an Autodesk.Revit.DB.Element object in Revit
/// </summary>
[SpeckleType("Objects.Data.GisObject")]
public class GisObject : Base, IGisObject
{
  public required string name { get; set; }
  public required int type { get; set; }
  public required IReadOnlyList<Base> displayValue { get; set; }
}
