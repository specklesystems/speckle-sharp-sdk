using Speckle.Objects.Other;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Data;

/// <summary>
/// Represents an Autodesk.AutoCAD.DatabaseServices.Entity object in AutoCAD.
/// </summary>
[SpeckleType("Objects.Data.AutocadObject")]
public class AutocadObject : DataObject, IAutocadObject
{
  public required string type { get; set; }

  public required string units { get; set; }

  /// <summary>
  /// Optional raw encoding (eg SAT) for lossless round-trip of solids/surfaces.
  /// </summary>
  public RawEncoding? rawEncoding { get; set; }

  IReadOnlyList<Base> IDisplayValue<IReadOnlyList<Base>>.displayValue => displayValue;
}
