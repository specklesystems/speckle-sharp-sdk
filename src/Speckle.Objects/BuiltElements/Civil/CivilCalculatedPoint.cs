using Speckle.Objects.Geometry;
using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements.Civil;

[SpeckleType("Objects.BuiltElements.Civil.CivilCalculatedPoint")]
public class CivilCalculatedPoint : Base, ICivilCalculatedObject
{
  public CivilCalculatedPoint() { }

  public CivilCalculatedPoint(
    Point point,
    List<string> codes,
    Vector normalToBaseline,
    Vector normalToSubassembly,
    Point stationOffsetElevationToBaseline
  )
  {
    this.point = point;
    this.codes = codes;
    this.normalToBaseline = normalToBaseline;
    this.normalToSubassembly = normalToSubassembly;
    this.stationOffsetElevationToBaseline = stationOffsetElevationToBaseline;
  }

  public Point point { get; set; }

  public List<string> codes { get; set; }

  public Vector normalToBaseline { get; set; }

  public Vector normalToSubassembly { get; set; }

  public Point stationOffsetElevationToBaseline { get; set; }
}
