using Speckle.Objects.Geometry;
using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements.Civil;

[SpeckleType("Objects.BuiltElements.Civil.CivilProfile")]
public class CivilProfile : Profile
{
  public string type { get; set; }

  public string style { get; set; }

  public double offset { get; set; }

  /// <summary>
  /// Points of vertical intersection
  /// </summary>
  public List<Point> pvis { get; set; }

  /// <summary>
  /// Name of parent profile if this is an offset profile
  /// </summary>
  public string parent { get; set; }
}
