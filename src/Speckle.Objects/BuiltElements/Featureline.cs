using Speckle.Objects.Geometry;
using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements;

[SpeckleType("Objects.BuiltElements.Featureline")]
public class Featureline : Base, IDisplayValue<List<Polyline>>
{
  /// <summary>
  /// The base curve of the featureline
  /// </summary>
  public ICurve curve { get; set; }

  /// <summary>
  /// The points constructing the Featureline
  /// </summary>
  /// <remarks>
  /// Can include both intersection and elevation points
  /// </remarks>
  public List<Point> points { get; set; }

  public string name { get; set; }

  public string units { get; set; }

  /// <summary>
  /// The 3D curves generated from the curve and points of the featureline
  /// </summary>
  [DetachProperty]
  public List<Polyline> displayValue { get; set; }
}
