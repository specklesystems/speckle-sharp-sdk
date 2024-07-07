using Objects.BuiltElements;
using Speckle.Core.Reflection;
using Speckle.Core.SchemaVersioning;

using SourceWall = Objects.Versions.V_0_1_0.BuiltElements.Wall;
using DestinationWall = Objects.BuiltElements.Wall;

namespace Speckle.Objects.Upgraders;

// POC: consider weakness of strings here
// the type passed in is a bit janky, if we want to move from say RevitWall to Wall,
// then we would need to have some care about where the type name came from, as in this example
// POC: the version could come from some constant tbh and then it won't be wrong...
// is the typename off the source or destination :pained face:
[NamedType(typeof(SourceWall), "0.1.0")]
public sealed class WallUpgrader : AbstractSchemaObjectBaseUpgrader<SourceWall, DestinationWall>
{
  public override DestinationWall Upgrade(SourceWall input)
  {
    // we need to clone the source and make a new destination
    
    
    
    
    // we need to fixup or add or otherwise
    
    
    return new DestinationWall();
  }
}
