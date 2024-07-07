using Speckle.Core.Reflection;
using Speckle.Core.SchemaVersioning;

using Source = Objects.Versions.V_0_1_0.Geometry.Point;
using Destination = Objects.Geometry.Point;

namespace Speckle.Objects.Versions.V_0_1_0.Upgraders;

// POC: consider weakness of strings here
// the type passed in is a bit janky, if we want to move from say RevitWall to Wall,
// then we would need to have some care about where the type name came from, as in this example
// POC: the version could come from some constant tbh and then it won't be wrong...
// is the typename off the source or destination :pained face:
[NamedType(typeof(Source), "0.1.0")]
public sealed class PointUpgrader : AbstractSchemaObjectBaseUpgrader<Source, Destination>
{
  public PointUpgrader() : base(new Version(0,1,0), new Version(0,2, 0))
  {
    
  }
  
  public override Destination Upgrade(Source input)
  {
    return new Destination(input.x, input.y, input.z);
  }
}
