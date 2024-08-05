using Speckle.Objects.Geometry;
using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements.TeklaStructures;

[SpeckleType("Objects.BuiltElements.TeklaStructures.Welds")]
public class Welds : Base
{
  [DetachProperty]
  public List<Mesh> displayValue { get; set; }

  public string mainObjectId { get; set; }
  public string secondaryObjectId { get; set; }
  public double sizeAbove { get; set; }
  public double sizeBelow { get; set; }
  public double lengthAbove { get; set; }
  public double lengthBelow { get; set; }
  public double pitchAbove { get; set; }
  public double pitchBelow { get; set; }
  public double angleAbove { get; set; } // In degrees
  public double angleBelow { get; set; } // In degrees
  public TeklaWeldType typeAbove { get; set; }
  public TeklaWeldType typeBelow { get; set; }
  public TeklaWeldIntermittentType intermittentType { get; set; }
}

[SpeckleType("Objects.BuiltElements.TeklaStructures.PolygonWelds")]
public class PolygonWelds : Welds
{
  public Polyline polyline { get; set; }
}
