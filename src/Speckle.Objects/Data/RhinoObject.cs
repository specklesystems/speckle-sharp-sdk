using Speckle.Objects.Other;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Data;

/// <summary>
/// Represents a Rhino.DocObjects.RhinoObject object in Rhinoceros 3D
/// </summary>
[SpeckleType("Objects.Data.RhinoObject")]
public class RhinoObject : DataObject, IRhinoObject
{
  public required string type { get; set; }

  public required string units { get; set; }

  [DetachProperty]
  public RawEncoding? rawEncoding { get; set; }

  IReadOnlyList<Base> IDisplayValue<IReadOnlyList<Base>>.displayValue => displayValue;
}
