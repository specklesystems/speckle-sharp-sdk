using Speckle.Objects.BuiltElements;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Structural.GSA.Geometry;

[SpeckleType("Objects.Structural.GSA.Geometry.GSAGridLine")]
public class GSAGridLine : GridLine
{
  public GSAGridLine() { }

  [SchemaInfo("GSAGridLine", "Creates a Speckle structural grid line for GSA", "GSA", "Geometry")]
  public GSAGridLine(int nativeId, string name, ICurve line)
  {
    this.nativeId = nativeId;
    label = name;
    baseLine = line;
  }

  public int nativeId { get; set; }
}
