using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements.Civil;

public class CivilCalculatedLink : Base, ICivilCalculatedObject
{
  public CivilCalculatedLink() { }

  public CivilCalculatedLink(List<string> codes, List<CivilCalculatedPoint> points)
  {
    this.codes = codes;
    this.points = points;
  }

  public List<string> codes { get; set; }

  [DetachProperty]
  public List<CivilCalculatedPoint> points { get; set; }
}
