using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements.Civil;

[SpeckleType("Objects.BuiltElements.Civil.CivilAppliedAssembly")]
public class CivilAppliedAssembly : Base
{
  public CivilAppliedAssembly() { }

  public CivilAppliedAssembly(
    List<CivilAppliedSubassembly> appliedSubassemblies,
    double adjustedElevation,
    string units
  )
  {
    this.appliedSubassemblies = appliedSubassemblies;
    this.adjustedElevation = adjustedElevation;
    this.units = units;
  }

  public List<CivilAppliedSubassembly> appliedSubassemblies { get; set; }

  public double adjustedElevation { get; set; }

  public string units { get; set; }
}
