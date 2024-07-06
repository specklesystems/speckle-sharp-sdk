using Objects.BuiltElements;
using Objects.BuiltElements.Revit;
using Speckle.Core.Reflection;
using Speckle.Core.SchemaVersioning;

namespace Speckle.Objects.Upgraders;

// POC: consider weakness of strings here
// the type passed in is a bit janky, if we want to move from say RevitWall to Wall,
// then we would need to have some care about where the type name came from, as in this example
// POC: the version could come from some constant tbh and then it won't be wrong...
[NamedType(typeof(Wall), "0.1.0")]
public sealed class WallUpgrader : AbstractSchemaObjectBaseUpgrader<Wall, RevitWall>
{
  public override RevitWall Upgrade(Wall input)
  {
    return Upgrade((Wall)input);
  }
}
