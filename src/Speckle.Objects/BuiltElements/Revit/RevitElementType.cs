using Speckle.Objects.Geometry;
using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements.Revit;

[SpeckleType("Objects.BuiltElements.Revit.RevitElementType")]
public class RevitElementType : Base
{
  public string family { get; set; }

  public string type { get; set; }

  public string category { get; set; }
}

[SpeckleType("Objects.BuiltElements.Revit.RevitMepElementType")]
public class RevitMepElementType : RevitElementType
{
  public string shape { get; set; }
}

/// <summary>
/// Represents the FamilySymbol subclass of ElementType in Revit
/// </summary>
[SpeckleType("Objects.BuiltElements.Revit.RevitSymbolElementType")]
public class RevitSymbolElementType : RevitElementType, IDisplayValue<List<Mesh>>
{
  /// <summary>
  /// The type of placement for this family symbol
  /// </summary>
  /// <remarks> See https://www.revitapidocs.com/2023/2abb8627-1da3-4069-05c9-19e4be5e02ad.htm </remarks>
  public string placementType { get; set; }

  /// <summary>
  /// Subcomponents found in this family symbol
  /// </summary>
  [DetachProperty]
  public List<Base> elements { get; set; }

  [DetachProperty]
  public List<Mesh> displayValue { get; set; }
}
