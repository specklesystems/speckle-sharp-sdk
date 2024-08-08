using Speckle.Objects.BuiltElements.Revit;
using Speckle.Objects.Utils;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Other.Revit;

/// <summary>
/// Material in Revit defininf all revit properties from Autodesk.Revit.DB.Material
/// </summary>
[SpeckleType("Objects.Other.Revit.RevitMaterial")]
public class RevitMaterial : Material
{
  public RevitMaterial() { }

  [SchemaInfo("RevitMaterial", "Creates a Speckle material", "Revit", "Architecture")]
  public RevitMaterial(
    string name,
    string category,
    string materialclass,
    int shiny,
    int smooth,
    int transparent,
    List<Parameter>? parameters = null
  )
  {
    this.parameters = parameters?.ToBase();
    this.name = name;
    materialCategory = category;
    materialClass = materialclass;
    shininess = shiny;
    smoothness = smooth;
    transparency = transparent;
  }

  public string materialCategory { get; set; }
  public string materialClass { get; set; }

  public int shininess { get; set; }
  public int smoothness { get; set; }
  public int transparency { get; set; }

  public Base? parameters { get; set; }
}
