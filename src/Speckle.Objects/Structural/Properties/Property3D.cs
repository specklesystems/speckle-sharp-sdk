using Speckle.Core.Kits;
using Speckle.Core.Models;
using Speckle.Objects.Structural.Geometry;
using Speckle.Objects.Structural.Materials;

namespace Speckle.Objects.Structural.Properties;

public class Property3D : Property
{
  public Property3D() { }

  [SchemaInfo("Property3D (by name)", "Creates a Speckle structural 3D element property", "Structural", "Properties")]
  public Property3D(string name)
  {
    this.name = name;
  }

  [SchemaInfo("Property3D", "Creates a Speckle structural 3D element property", "Structural", "Properties")]
  public Property3D(string name, PropertyType3D type, StructuralMaterial material)
  {
    this.name = name;
    this.type = type;
    this.material = material;
  }

  public PropertyType3D type { get; set; }

  [DetachProperty]
  public StructuralMaterial material { get; set; }

  [DetachProperty]
  public Axis orientationAxis { get; set; }
}
