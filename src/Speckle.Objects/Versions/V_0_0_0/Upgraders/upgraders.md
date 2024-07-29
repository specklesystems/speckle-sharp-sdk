# Upgraders
Add upgraders for a given version with the V_Mj_Mi_Bf folder structure.
This is not mandatory and no special naming is required for the upgrader to be found.

Upgraders look something like the below:

```csharp
namespace Speckle.Objects.Versions.V_0_0_0.Upgraders;

// POC: consider weakness of strings here
// the type passed in is a bit janky, if we want to move from say RevitWall to Wall,
// then we would need to have some care about where the type name came from, as in this example
// POC: the version could come from some constant tbh and then it won't be wrong...
// is the typename off the source or destination :pained face:
[NamedType(typeof(Source), "0.0.0")]
public sealed class PointUpgrader : AbstractSchemaObjectBaseUpgrader<Source, Destination>
{
  public PointUpgrader() : base(new Version(0,0,0), new Version(0,1, 0))
  {
    
  }
  
  public override Destination Upgrade(Source input)
  {
    return new Destination(input.x, input.y, input.z);
  }
}
```

The Upgrader implements `AbstractSchemaObjectBaseUpgrader` which in turn implements `ISchemaObjectUpgrader` taking type args
for `Source` and `Destinaton` that match the base types provide for the registered `ISchmeaObjectUpgraderManager`
`NamedTypeAttribute` and signals to this manager that this is an upgrader type to be registered. The NamedTypeAttribute is agnostic
of upgrading but allows the types to be named and include a suffix. The Manager uses this to construct a strong key,
so that an upgrader for a given type can be found for a given version to upgrade _from_.

To upgrade an object, it is only necessary to overrider the Upgrade() method which will accept the strong types provided
for this Upgrader.

Note that the design should permit for the upgrading of Destination types that are have different base implementations
i.e. in the long run a RevitWall could become a SpeckleWall, with the former being derived from Base and the latter knowing
nothing about it, perhaps implementing ISpeckleObject instead.
