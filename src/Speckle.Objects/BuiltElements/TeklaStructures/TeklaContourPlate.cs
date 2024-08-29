using Speckle.Objects.Geometry;
using Speckle.Objects.Structural.Materials;
using Speckle.Objects.Structural.Properties.Profiles;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements.TeklaStructures;

[SpeckleType("Objects.BuiltElements.TeklaStructures.TeklaContourPlate")]
public class TeklaContourPlate : Area
{
  [SchemaInfo("ContourPlate", "Creates a TeklaStructures contour plate.", "Tekla", "Structure")]
  public TeklaContourPlate(
    SectionProfile profile,
    Polyline outline,
    string finish,
    string classNumber,
    string units,
    StructuralMaterial? material = null,
    TeklaPosition? position = null,
    Base? rebars = null
  )
  {
    this.profile = profile;
    this.outline = outline;
    this.material = material;
    this.finish = finish;
    this.classNumber = classNumber;
    this.position = position ?? new();
    this.rebars = rebars;
    this.units = units;
  }

  public TeklaContourPlate() { }

  [DetachProperty]
  public SectionProfile profile { get; set; }

  [DetachProperty]
  public StructuralMaterial? material { get; set; }

  [DetachProperty]
  public string finish { get; set; }

  [DetachProperty]
  public string classNumber { get; set; }

  [DetachProperty]
  public TeklaPosition position { get; set; } = new();

  [DetachProperty]
  public Base? rebars { get; set; }

  public List<TeklaContourPoint> contour { get; set; } // Use for ToNative to Tekla. Other programs can use Area.outline.
}

[SpeckleType("Objects.BuiltElements.TeklaStructures.TeklaContourPoint")]
public class TeklaContourPoint : Point
{
  public TeklaContourPoint() { }

  public TeklaContourPoint(Point point) { }

  public TeklaChamferType chamferType { get; set; }
  public double xDim { get; set; }
  public double yDim { get; set; }
  public double dz1 { get; set; }
  public double dz2 { get; set; }
}
