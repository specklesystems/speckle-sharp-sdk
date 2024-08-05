using Speckle.Objects.Geometry;
using Speckle.Objects.Other.Civil;
using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements.Civil;

public class CivilAppliedSubassembly : Base
{
  public CivilAppliedSubassembly() { }

  public CivilAppliedSubassembly(
    string subassemblyId,
    string subassemblyName,
    List<CivilCalculatedShape> shapes,
    Point stationOffsetElevationToBaseline,
    List<CivilDataField> parameters
  )
  {
    this.subassemblyId = subassemblyId;
    this.subassemblyName = subassemblyName;
    this.shapes = shapes;
    this.stationOffsetElevationToBaseline = stationOffsetElevationToBaseline;
    this.parameters = parameters;
  }

  public string subassemblyId { get; set; }

  public string subassemblyName { get; set; }

  public List<CivilCalculatedShape> shapes { get; set; }

  public Point stationOffsetElevationToBaseline { get; set; }

  [DetachProperty]
  public List<CivilDataField> parameters { get; set; }
}
